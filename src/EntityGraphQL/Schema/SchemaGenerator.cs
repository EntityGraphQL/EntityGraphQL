using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace EntityGraphQL.Schema
{
    internal class SchemaGenerator
    {
        private static readonly Dictionary<Type, string> typeMapping = new Dictionary<Type, string> {
            {typeof(string), "String"},
            {typeof(RequiredField<string>), "String!"},
            {typeof(Guid), "ID"},
            {typeof(Guid?), "ID"},
            {typeof(RequiredField<Guid>), "ID!"},
            {typeof(int), "Int"},
            {typeof(RequiredField<int>), "Int!"},
            {typeof(int?), "Int"},
            {typeof(double), "Float"},
            {typeof(RequiredField<double>), "Float!"},
            {typeof(double?), "Float"},
            {typeof(float), "Float"},
            {typeof(RequiredField<float>), "Float!"},
            {typeof(float?), "Float"},
            {typeof(bool), "Boolean"},
            {typeof(bool?), "Boolean"},
            {typeof(RequiredField<bool>), "Boolean"},
            {typeof(EntityQueryType<>), "String"},
        };

        internal static string Make(ISchemaProvider schema)
        {
            var types = BuildSchemaTypes(schema);
            var mutations = BuildMutations(schema);

            var queryTypes = MakeQueryType(schema);

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

        private static string BuildMutations(ISchemaProvider schema)
        {
            var mutations = new StringBuilder();
            foreach (var item in schema.GetMutations())
            {
                if (!string.IsNullOrEmpty(item.Description))
                    mutations.AppendLine($"\t\"{item.Description}\"");

                mutations.AppendLine($"\t{ToCamelCase(item.Name)}{GetGqlArgs(item, schema, "()")}: {GetGqlReturnType(item, schema)}");
            }

            return mutations.ToString();
        }

        private static string BuildSchemaTypes(ISchemaProvider schema)
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

                    types.AppendLine($"\t{ToCamelCase(field.Name)}{GetGqlArgs(field, schema)}: {GetGqlReturnType(field, schema)}");

                }
                types.AppendLine("}");
            }

            return types.ToString();
        }

        private static object GetGqlReturnType(IMethodType field, ISchemaProvider schema)
        {
            return field.IsEnumerable ? "[" + ClrToGqlType(field.ReturnTypeClr.GetGenericArguments()[0], schema) + "]" : ClrToGqlType(field.ReturnTypeClr, schema);
        }

        private static object GetGqlArgs(IMethodType field, ISchemaProvider schema, string noArgs = "")
        {
            if (field.Arguments == null || !field.Arguments.Any())
                return noArgs;

            var all = field.Arguments.Select(f => ToCamelCase(f.Key) + ": " + ClrToGqlType(f.Value, schema));

            return $"({string.Join(", ", all)})";
        }

        private static string ClrToGqlType(Type type, ISchemaProvider schema)
        {
            if (!typeMapping.ContainsKey(type))
            {
                if (schema.HasType(type))
                {
                    return schema.GetSchemaTypeNameForRealType(type);
                }
                if (type.IsConstructedGenericType)
                {
                    return ClrToGqlType(type.GetGenericTypeDefinition(), schema);
                }
                // Default to a string type
                return "String";
            }
            return typeMapping[type];

        }

        private static string MakeQueryType(ISchemaProvider schema)
        {
            var sb = new StringBuilder();

            foreach (var t in schema.GetQueryFields().OrderBy(s => s.Name))
            {
                if (t.Name.StartsWith("__"))
                    continue;
                var typeName = GetGqlReturnType(t, schema);
                if (!string.IsNullOrEmpty(t.Description))
                    sb.AppendLine($"\t\"{t.Description}\"");
                sb.AppendLine($"\t{ToCamelCase(t.Name)}{GetGqlArgs(t, schema)}: {typeName}");
            }

            return sb.ToString();
        }

        private static object ToCamelCase(string name)
        {
            return name.Substring(0, 1).ToLowerInvariant() + name.Substring(1);
        }
    }
}