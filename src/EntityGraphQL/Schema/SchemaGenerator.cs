using System;
using System.Linq;
using System.Text;
using EntityGraphQL.Directives;

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
            var rootQueryType = schema.GetSchemaType(schema.QueryContextType, null);
            var mutationType = schema.GetSchemaType(schema.MutationType, null);

            var types = BuildSchemaTypes(schema);

            var schemaBuilder = new StringBuilder("schema {");
            schemaBuilder.AppendLine();
            schemaBuilder.AppendLine($"\tquery: {rootQueryType.Name}");
            if (mutationType.GetFields().Any())
                schemaBuilder.AppendLine($"\tmutation: {mutationType.Name}");
            schemaBuilder.AppendLine("}");

            schemaBuilder.AppendLine();

            foreach (var item in schema.GetScalarTypes().Distinct().OrderBy(t => t.Name))
            {
                schemaBuilder.AppendLine($"scalar {item.Name}");
            }
            schemaBuilder.AppendLine();

            foreach (var directive in schema.GetDirectives().OrderBy(t => t.Name))
            {
                schemaBuilder.AppendLine($"directive @{directive.Name}{GetDirectiveArgs(schema, directive)} on {string.Join(" | ", directive.On.Select(i => Enum.GetName(typeof(ExecutableDirectiveLocation), i)))}");
            }
            schemaBuilder.AppendLine();

            schemaBuilder.Append(BuildEnumTypes(schema));

            schemaBuilder.AppendLine(OutputSchemaType(schema, schema.GetSchemaType(schema.QueryContextName, null)));

            schemaBuilder.Append(types);

            if (schema.Mutation().SchemaType.GetFields().Any())
                schemaBuilder.AppendLine(OutputSchemaType(schema, schema.Mutation().SchemaType));

            return schemaBuilder.ToString();
        }

        private static string BuildEnumTypes(ISchemaProvider schema)
        {
            var types = new StringBuilder();
            foreach (var typeItem in schema.GetNonContextTypes().OrderBy(t => t.Name))
            {
                if (typeItem.Name.StartsWith("__") || !typeItem.IsEnum)
                    continue;

                if (!string.IsNullOrEmpty(typeItem.Description))
                    types.AppendLine($"\"\"\"{EscapeString(typeItem.Description)}\"\"\"");

                types.AppendLine($"enum {typeItem.Name} {{");
                foreach (var field in typeItem.GetFields().OrderBy(t => t.Name))
                {
                    if (field.Name.StartsWith("__"))
                        continue;

                    if (!string.IsNullOrEmpty(field.Description))
                        types.AppendLine($"\t\"\"\"{EscapeString(field.Description)}\"\"\"");

                    types.AppendLine($"\t{field.Name}{GetDeprecation(field)}");

                }
                types.AppendLine("}");
                types.AppendLine();
            }

            return types.ToString();
        }

        private static string BuildSchemaTypes(ISchemaProvider schema)
        {
            var types = new StringBuilder();
            foreach (var typeItem in schema.GetNonContextTypes().OrderBy(t => t.Name))
            {
                if (typeItem.Name.StartsWith("__") || typeItem.IsEnum || typeItem.IsScalar || typeItem.Name == schema.Mutation().SchemaType.Name)
                    continue;

                if (typeItem.GetFields().Any() || (typeItem.GqlType == GqlTypeEnum.Union && typeItem.PossibleTypes.Count() > 0))
                {
                    types.AppendLine(OutputSchemaType(schema, typeItem));
                }
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

        private static object GetDirectiveArgs(ISchemaProvider schema, IDirectiveProcessor directive)
        {
            var args = directive.GetArguments(schema);
            if (args == null || !args.Any())
                return string.Empty;

            var allArgs = string.Join(", ", args.Select(f => f.Name + ": " + f.Type.GqlTypeForReturnOrArgument));
            return string.IsNullOrEmpty(allArgs) ? string.Empty : $"({allArgs})";
        }

        private static string OutputSchemaType(ISchemaProvider schema, ISchemaType schemaType)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(schemaType.Description))
                sb.AppendLine($"\"\"\"{EscapeString(schemaType.Description)}\"\"\"");


            if (schemaType.GqlType == GqlTypeEnum.Union)
            {
                if(schemaType.PossibleTypes.Count == 0)
                {
                    return string.Empty;
                }

                sb.AppendLine($"union {schemaType.Name} = {string.Join(" | ", schemaType.PossibleTypes.Select(i => i.Name))}");
                return sb.ToString();
            }

            var type = schemaType.GqlType switch
            {
                GqlTypeEnum.Input=> "input",
                GqlTypeEnum.Interface => "interface",
                GqlTypeEnum.Union => "union",
                _ => "type"
            };

            var implements = "";
            if (schemaType.BaseTypes != null && schemaType.BaseTypes.Count() > 0)
            {
                implements += $"implements {string.Join(" & ", schemaType.BaseTypes.Select(i => i.Name))} ";
            }

            var oneOf = schemaType.IsOneOf ? "@oneOf " : "";

            sb.AppendLine($"{type} {schemaType.Name} {implements}{oneOf}{{");

            foreach (var field in schemaType.GetFields().OrderBy(s => s.Name))
            {
                if (field.Name.StartsWith("__"))
                    continue;
                if (!string.IsNullOrEmpty(field.Description))
                    sb.AppendLine($"\t\"\"\"{EscapeString(field.Description)}\"\"\"");
                sb.AppendLine($"\t{schema.SchemaFieldNamer(field.Name)}{GetGqlArgs(schema, field)}: {field.ReturnType.GqlTypeForReturnOrArgument}{GetDeprecation(field)}");
            }
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}