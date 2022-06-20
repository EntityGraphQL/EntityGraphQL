using System.Linq;
using System.Text;

namespace EntityGraphQL.Schema
{
    public class SchemaGenerator
    {
        internal static string EscapeString(string? input)
        {
            if (input == null)
                return string.Empty;
            return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        internal static string Make(ISchemaProvider schema)
        {
            var scalars = new StringBuilder();

            var rootQueryType = schema.GetSchemaType(schema.QueryContextType, null);
            var mutationType = schema.GetSchemaType(schema.MutationType, null);

            foreach (var item in schema.GetScalarTypes().Distinct())
            {
                scalars.AppendLine($"scalar {item.Name}");
            }

            var enums = BuildEnumTypes(schema);
            var types = BuildSchemaTypes(schema);
            var hasMutations = mutationType.GetFields().Any();

            var queryTypes = MakeQueryType(schema);

            var schemaStr = $@"schema {{
    query: {rootQueryType.Name}
    {(hasMutations ? "mutation: " + mutationType.Name : "")}
}}

{scalars}
{enums}

type {rootQueryType.Name} {{
{queryTypes}
}}
{types}
";
            return schemaStr;
        }

        private static string BuildEnumTypes(ISchemaProvider schema)
        {
            var types = new StringBuilder();
            foreach (var typeItem in schema.GetNonContextTypes())
            {
                if (typeItem.Name.StartsWith("__") || !typeItem.IsEnum)
                    continue;

                types.AppendLine();
                if (!string.IsNullOrEmpty(typeItem.Description))
                    types.AppendLine($"\t\"\"\"{EscapeString(typeItem.Description)}\"\"\"");

                types.AppendLine($"enum {typeItem.Name} {{");
                foreach (var field in typeItem.GetFields())
                {
                    if (field.Name.StartsWith("__"))
                        continue;

                    if (!string.IsNullOrEmpty(field.Description))
                        types.AppendLine($"\t\"\"\"{EscapeString(field.Description)}\"\"\"");

                    types.AppendLine($"\t{field.Name}{GetDeprecation(field)}");

                }
                types.AppendLine("}");
            }

            return types.ToString();
        }

        private static string BuildSchemaTypes(ISchemaProvider schema)
        {
            var types = new StringBuilder();
            foreach (var typeItem in schema.GetNonContextTypes())
            {
                if (typeItem.Name.StartsWith("__") || typeItem.IsEnum || typeItem.IsScalar)
                    continue;

                if (!typeItem.GetFields().Any())
                    continue;

                types.AppendLine();
                if (!string.IsNullOrEmpty(typeItem.Description))
                    types.AppendLine($"\"\"\"{EscapeString(typeItem.Description)}\"\"\"");

                var type = typeItem switch
                {
                    { IsInput: true } => "input",
                    { IsInterface: true } => "interface",
                    _ => "type"
                };

                var implements = string.IsNullOrWhiteSpace(typeItem.BaseType)
                    ? ""
                    : $"implements {typeItem.BaseType} ";

                types.AppendLine($"{type} {typeItem.Name} {implements}{{");
                foreach (var field in typeItem.GetFields())
                {
                    if (field.Name.StartsWith("__"))
                        continue;

                    if (!string.IsNullOrEmpty(field.Description))
                        types.AppendLine($"\t\"\"\"{EscapeString(field.Description)}\"\"\"");

                    types.AppendLine($"\t{schema.SchemaFieldNamer(field.Name)}{GetGqlArgs(schema, field)}: {field.ReturnType.GqlTypeForReturnOrArgument}{GetDeprecation(field)}");
                }
                types.AppendLine("}");
            }

            return types.ToString();
        }

        private static object GetDeprecation(IField field)
        {
            if (!field.IsDeprecated)
                return string.Empty;

            return $" @deprecated(reason: \"{EscapeString(field.DeprecationReason)}\")";
        }

        private static object GetGqlArgs(ISchemaProvider schema, IField field, string noArgs = "")
        {
            if (field.Arguments == null || !field.Arguments.Any() || field.ArgumentsAreInternal)
                return noArgs;

            var all = field.Arguments.Select(f => schema.SchemaFieldNamer(f.Key) + ": " + f.Value.Type.GqlTypeForReturnOrArgument);

            var args = string.Join(", ", all);
            return string.IsNullOrEmpty(args) ? string.Empty : $"({args})";
        }

        private static string MakeQueryType(ISchemaProvider schema)
        {
            var sb = new StringBuilder();

            foreach (var t in schema.Type(schema.QueryContextName).GetFields().OrderBy(s => s.Name))
            {
                if (t.Name.StartsWith("__"))
                    continue;
                if (!string.IsNullOrEmpty(t.Description))
                    sb.AppendLine($"\t\"\"\"{EscapeString(t.Description)}\"\"\"");
                sb.AppendLine($"\t{schema.SchemaFieldNamer(t.Name)}{GetGqlArgs(schema, t)}: {t.ReturnType.GqlTypeForReturnOrArgument}");
            }

            return sb.ToString();
        }
    }
}