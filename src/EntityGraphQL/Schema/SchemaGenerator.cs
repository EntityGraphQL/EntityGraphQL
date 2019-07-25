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
        };

        internal static string Make(ISchemaProvider schema, IReadOnlyDictionary<Type, string> typeMappings)
        {
            // defaults first
            var combinedMapping = DefaultTypeMappings.ToDictionary(k => k.Key, v => v.Value);
            foreach (var item in typeMappings)
            {
                if (combinedMapping.ContainsKey(item.Key))
                    combinedMapping[item.Key] = item.Value;
                else
                    combinedMapping.Add(item.Key, item.Value);
            }

            var types = BuildSchemaTypes(schema, combinedMapping);
            var mutations = BuildMutations(schema, combinedMapping);

            var queryTypes = MakeQueryType(schema, combinedMapping);

            return $@"schema {{
    query: RootQuery
    mutation: Mutation
}}

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
                types.AppendLine();
                // if (!string.IsNullOrEmpty(typeItem.Description))
                //     types.AppendLine($"\"{typeItem.Description}\"");

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
                if (type.IsEnumerableOrArray()) {
                    return "[" + ClrToGqlType(type.GetGenericArguments()[0], schema, combinedMapping) + "]";
                }
                if (type.IsConstructedGenericType)
                {
                    return ClrToGqlType(type.GetGenericTypeDefinition(), schema, combinedMapping);
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