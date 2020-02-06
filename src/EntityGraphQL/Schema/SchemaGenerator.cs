using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema
{
    public class SchemaGenerator
    {
        public static readonly Dictionary<Type, string> DefaultTypeMappings = new Dictionary<Type, string> {
            {typeof(string), "String"},
            {typeof(RequiredField<string>), "String!"},

            {typeof(Guid), "ID!"},
            {typeof(Guid?), "ID"},
            {typeof(RequiredField<Guid>), "ID!"},

            {typeof(int), "Int!"},
            {typeof(int?), "Int"},
            {typeof(RequiredField<int>), "Int!"},

            {typeof(Int16), "Int!"},
            {typeof(Int16?), "Int"},
            {typeof(RequiredField<Int16>), "Int!"},

            {typeof(double), "Float!"},
            {typeof(double?), "Float"},
            {typeof(RequiredField<double>), "Float!"},

            {typeof(float), "Float!"},
            {typeof(float?), "Float"},
            {typeof(RequiredField<float>), "Float!"},

            {typeof(Decimal), "Float!"},
            {typeof(Decimal?), "Float"},
            {typeof(RequiredField<Decimal>), "Float!"},

            {typeof(bool), "Boolean!"},
            {typeof(bool?), "Boolean"},
            {typeof(RequiredField<bool>), "Boolean!"},

            {typeof(EntityQueryType<>), "String"},

            {typeof(long), "Int!"},
            {typeof(long?), "Int"},
            {typeof(RequiredField<long>), "Int!"},

            {typeof(DateTime), "String!"},
            {typeof(DateTime?), "String"},
            {typeof(RequiredField<DateTime>), "String!"},

            {typeof(uint), "Int!"},
            {typeof(uint?), "Int"},
            {typeof(RequiredField<uint>), "Int!"},

            {typeof(UInt16), "Int!"},
            {typeof(UInt16?), "Int"},
            {typeof(RequiredField<UInt16>), "Int!"},
        };

        internal static string Make(ISchemaProvider schema, IReadOnlyDictionary<Type, string> typeMappings, Dictionary<Type, string> customScalarMapping)
        {
            // defaults first
            var combinedMapping = DefaultTypeMappings.ToDictionary(k => k.Key, v => v.Value);
            foreach (var item in typeMappings)
            {
                combinedMapping[item.Key] = item.Value;
            }

            var scalars = new StringBuilder();

            foreach (var item in customScalarMapping.Select(i => i.Value).Distinct())
            {
                scalars.AppendLine($"scalar {item}");
            }

            var enums = BuildEnumTypes(schema, combinedMapping);
            var types = BuildSchemaTypes(schema, combinedMapping);
            var mutations = BuildMutations(schema, combinedMapping);
            var hasMutations = mutations.Any();

            var queryTypes = MakeQueryType(schema, combinedMapping);

            var schemaStr = $@"schema {{
    query: RootQuery
    {(hasMutations ? "mutation: Mutation" : "")}
}}

{scalars}
{enums}

type RootQuery {{
{queryTypes}
}}
{types}
";
            if (hasMutations)
            {
                schemaStr += $@"type Mutation {{
{mutations}
}}";
            }
            return schemaStr;
        }

        private static string BuildMutations(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            var mutations = new StringBuilder();
            foreach (var item in schema.GetMutations())
            {
                if (!string.IsNullOrEmpty(item.Description))
                    mutations.AppendLine($"\t\"{item.Description}\"");

                mutations.AppendLine($"\t{ToCamelCaseStartsLower(item.Name)}{GetGqlArgs(item, schema, combinedMapping, "()")}: {GetGqlReturnType(item, schema, combinedMapping)}");
            }

            return mutations.ToString();
        }

        private static string BuildEnumTypes(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            var types = new StringBuilder();
            foreach (var typeItem in schema.GetNonContextTypes())
            {
                if (typeItem.Name.StartsWith("__") || !typeItem.IsEnum)
                    continue;

                types.AppendLine();
                if (!string.IsNullOrEmpty(typeItem.Description))
                    types.AppendLine($"\t\"{typeItem.Description}\"");

                types.AppendLine($"enum {typeItem.Name} {{");
                foreach (var field in typeItem.GetFields())
                {
                    if (field.Name.StartsWith("__"))
                        continue;

                    if (!string.IsNullOrEmpty(field.Description))
                        types.AppendLine($"\t\"{field.Description}\"");

                    types.AppendLine($"\t{field.Name}");

                }
                types.AppendLine("}");
            }

            return types.ToString();
        }

        private static string BuildSchemaTypes(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            var types = new StringBuilder();
            foreach (var typeItem in schema.GetNonContextTypes())
            {
                if (typeItem.Name.StartsWith("__") || typeItem.IsEnum)
                    continue;

                types.AppendLine();
                if (!string.IsNullOrEmpty(typeItem.Description))
                    types.AppendLine($"\"{typeItem.Description}\"");

                types.AppendLine($"{(typeItem.IsInput ? "input" : "type")} {typeItem.Name} {{");
                foreach (var field in typeItem.GetFields())
                {
                    if (field.Name.StartsWith("__"))
                        continue;

                    if (!string.IsNullOrEmpty(field.Description))
                        types.AppendLine($"\t\"{field.Description}\"");

                    types.AppendLine($"\t{ToCamelCaseStartsLower(field.Name)}{GetGqlArgs(field, schema, combinedMapping)}: {GetGqlReturnType(field, schema, combinedMapping)}");

                }
                types.AppendLine("}");
            }

            return types.ToString();
        }

        private static object GetGqlReturnType(IMethodType field, ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            return ClrToGqlType(field.ReturnTypeNotNullable, field.ReturnElementTypeNullable, field.ReturnTypeClr, schema, combinedMapping);
        }

        private static object GetGqlArgs(IMethodType field, ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping, string noArgs = "")
        {
            if (field.Arguments == null || !field.Arguments.Any())
                return noArgs;

            var all = field.Arguments.Select(f => ToCamelCaseStartsLower(f.Key) + ": " + ClrToGqlType(f.Value.TypeNotNullable, false, f.Value.Type, schema, combinedMapping));

            return $"({string.Join(", ", all)})";
        }

        private static string ClrToGqlType(bool typeNotNullable, bool returnElementTypeNullable, Type type, ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            string gqlType = null;
            if (!combinedMapping.ContainsKey(type))
            {
                if (schema.HasType(type))
                {
                    gqlType = schema.GetSchemaTypeNameForClrType(type);
                }
                else if (type.IsEnumerableOrArray())
                {
                    gqlType = "[" + ClrToGqlType(!returnElementTypeNullable, false, type.GetEnumerableOrArrayType(), schema, combinedMapping) + "]";
                }
                else if (type.IsNullableType())
                {
                    gqlType = ClrToGqlType(typeNotNullable, returnElementTypeNullable, Nullable.GetUnderlyingType(type), schema, combinedMapping);
                }
                else if (type.IsConstructedGenericType)
                {
                    gqlType = ClrToGqlType(typeNotNullable, returnElementTypeNullable, type.GetGenericTypeDefinition(), schema, combinedMapping);
                }
                else if (type.GetTypeInfo().IsEnum)
                {
                    gqlType = "Int";
                }
                else
                {
                    // Default to a string type
                    gqlType = "String";
                }
            }
            else
            {
                gqlType = combinedMapping[type];
            }
            if (typeNotNullable && !gqlType.EndsWith("!"))
            {
                gqlType += "!";
            }
            return gqlType;
        }

        private static string MakeQueryType(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            var sb = new StringBuilder();

            foreach (var t in schema.GetQueryFields().OrderBy(s => s.Name))
            {
                if (t.Name.StartsWith("__"))
                    continue;
                var typeName = GetGqlReturnType(t, schema, combinedMapping);
                if (!string.IsNullOrEmpty(t.Description))
                    sb.AppendLine($"\t\"{t.Description}\"");
                sb.AppendLine($"\t{ToCamelCaseStartsLower(t.Name)}{GetGqlArgs(t, schema, combinedMapping)}: {typeName}");
            }

            return sb.ToString();
        }

        public static string ToCamelCaseStartsLower(string name)
        {
            return name.Substring(0, 1).ToLowerInvariant() + name.Substring(1);
        }
    }
}