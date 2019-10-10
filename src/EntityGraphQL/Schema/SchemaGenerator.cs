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

            {typeof(Guid), "ID"},
            {typeof(Guid?), "ID"},
            {typeof(RequiredField<Guid>), "ID!"},

            {typeof(int), "Int"},
            {typeof(int?), "Int"},
            {typeof(RequiredField<int>), "Int!"},

            {typeof(Int16), "Int"},
            {typeof(Int16?), "Int"},
            {typeof(RequiredField<Int16>), "Int!"},

            {typeof(double), "Float"},
            {typeof(double?), "Float"},
            {typeof(RequiredField<double>), "Float!"},

            {typeof(float), "Float"},
            {typeof(float?), "Float"},
            {typeof(RequiredField<float>), "Float!"},

            {typeof(bool), "Boolean"},
            {typeof(bool?), "Boolean"},
            {typeof(RequiredField<bool>), "Boolean!"},

            {typeof(EntityQueryType<>), "String"},

            {typeof(long), "Int"},
            {typeof(long?), "Int"},
            {typeof(RequiredField<long>), "Int!"},

            {typeof(DateTime), "String"},
            {typeof(DateTime?), "String"},
            {typeof(RequiredField<DateTime>), "String!"},

            {typeof(uint), "Int"},
            {typeof(uint?), "Int"},
            {typeof(RequiredField<uint>), "Int!"},

            {typeof(UInt16), "Int"},
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
            foreach (var item in customScalarMapping)
            {
                combinedMapping[item.Key] = item.Value;
            }

            foreach (var item in customScalarMapping.Select(i => i.Value).Distinct())
            {
                scalars.AppendLine($"scalar {item}");
            }

            var types = BuildSchemaTypes(schema, combinedMapping);
            var mutations = BuildMutations(schema, combinedMapping);

            var queryTypes = MakeQueryType(schema, combinedMapping);

            return $@"schema {{
    query: RootQuery
    mutation: Mutation
}}

{scalars}

type RootQuery {{
{queryTypes}
}}
{types}

type Mutation {{
{mutations}
}}";
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

        private static string BuildSchemaTypes(ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            var types = new StringBuilder();
            foreach (var typeItem in schema.GetNonContextTypes())
            {
                if (typeItem.Name.StartsWith("__"))
                    continue;

                types.AppendLine();
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
            return field.IsEnumerable ? "[" + ClrToGqlType(field.ReturnTypeClr.GetEnumerableOrArrayType(), schema, combinedMapping) + "]" : ClrToGqlType(field.ReturnTypeClr, schema, combinedMapping);
        }

        private static object GetGqlArgs(IMethodType field, ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping, string noArgs = "")
        {
            if (field.Arguments == null || !field.Arguments.Any())
                return noArgs;

            var all = field.Arguments.Select(f => ToCamelCaseStartsLower(f.Key) + ": " + ClrToGqlType(f.Value, schema, combinedMapping));

            return $"({string.Join(", ", all)})";
        }

        private static string ClrToGqlType(Type type, ISchemaProvider schema, IReadOnlyDictionary<Type, string> combinedMapping)
        {
            if (!combinedMapping.ContainsKey(type))
            {
                if (schema.HasType(type))
                {
                    return schema.GetSchemaTypeNameForRealType(type);
                }
                if (type.IsEnumerableOrArray())
                {
                    return "[" + ClrToGqlType(type.GetGenericArguments()[0], schema, combinedMapping) + "]";
                }
                if (type.IsConstructedGenericType)
                {
                    return ClrToGqlType(type.GetGenericTypeDefinition(), schema, combinedMapping);
                }
                if (type.GetTypeInfo().IsEnum)
                {
                    return "Int";
                }
                // Default to a string type
                return "String";
            }
            return combinedMapping[type];

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