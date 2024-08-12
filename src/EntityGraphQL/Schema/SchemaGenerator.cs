using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using EntityGraphQL.Directives;
using EntityGraphQL.Schema.Directives;

// can remove this when we drop netstandard2.1
#pragma warning disable CA1305
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
            var rootQueryType = schema.GetSchemaType(schema.QueryContextType, false, null);
            var mutationType = schema.Mutation().SchemaType;
            var subscriptionType = schema.Subscription().SchemaType;

            var types = BuildSchemaTypes(schema);

            var schemaBuilder = new StringBuilder("schema {");
            schemaBuilder.AppendLine();
            schemaBuilder.AppendLine($"\tquery: {rootQueryType.Name}");
            bool outputMutation = mutationType.GetFields().Any(f => !f.Name.StartsWith("__", StringComparison.InvariantCulture));
            bool outputSubscription = subscriptionType.GetFields().Any(f => !f.Name.StartsWith("__", StringComparison.InvariantCulture));
            if (outputMutation)
                schemaBuilder.AppendLine($"\tmutation: {mutationType.Name}");
            if (outputSubscription)
                schemaBuilder.AppendLine($"\tsubscription: {subscriptionType.Name}");
            schemaBuilder.AppendLine("}");

            schemaBuilder.AppendLine();

            foreach (var item in schema.GetScalarTypes().Distinct().OrderBy(t => t.Name))
            {
                if (!string.IsNullOrEmpty(item.Description))
                    schemaBuilder.AppendLine($"\"\"\"{EscapeString(item.Description)}\"\"\"");
                schemaBuilder.AppendLine($"scalar {item.Name}{GetDirectives(item.Directives)}");
            }
            schemaBuilder.AppendLine();

            foreach (var directive in schema.GetDirectives().OrderBy(t => t.Name))
            {
                if (!string.IsNullOrEmpty(directive.Description))
                    schemaBuilder.AppendLine($"\"\"\"{EscapeString(directive.Description)}\"\"\"");
                schemaBuilder.AppendLine(
                    $"directive @{directive.Name}{GetDirectiveArgs(schema, directive)} on {string.Join(" | ", directive.Location.Select(i => Enum.GetName(typeof(ExecutableDirectiveLocation), i)))}"
                );
            }
            schemaBuilder.AppendLine();

            schemaBuilder.Append(BuildEnumTypes(schema));

            schemaBuilder.AppendLine(OutputSchemaType(schema, schema.GetSchemaType(schema.QueryContextName, null)));

            schemaBuilder.Append(types);

            if (outputMutation)
                schemaBuilder.AppendLine(OutputSchemaType(schema, schema.Mutation().SchemaType));
            if (outputSubscription)
                schemaBuilder.AppendLine(OutputSchemaType(schema, schema.Subscription().SchemaType));

            return schemaBuilder.ToString();
        }

        private static string BuildEnumTypes(ISchemaProvider schema)
        {
            var types = new StringBuilder();
            foreach (var typeItem in schema.GetNonContextTypes().OrderBy(t => t.Name))
            {
                if (typeItem.Name.StartsWith("__", StringComparison.InvariantCulture) || !typeItem.IsEnum)
                    continue;

                if (!string.IsNullOrEmpty(typeItem.Description))
                    types.AppendLine($"\"\"\"{EscapeString(typeItem.Description)}\"\"\"");

                types.AppendLine($"enum {typeItem.Name} {{");
                foreach (var field in typeItem.GetFields().OrderBy(t => t.Name))
                {
                    if (field.Name.StartsWith("__", StringComparison.InvariantCulture))
                        continue;

                    if (!string.IsNullOrEmpty(field.Description))
                        types.AppendLine($"\t\"\"\"{EscapeString(field.Description)}\"\"\"");

                    types.AppendLine($"\t{field.Name}{GetDirectives(field.DirectivesReadOnly)}");
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
                if (
                    typeItem.Name.StartsWith("__", StringComparison.InvariantCulture)
                    || typeItem.IsEnum
                    || typeItem.IsScalar
                    || typeItem.Name == schema.Mutation().SchemaType.Name
                    || typeItem.Name == schema.Subscription().SchemaType.Name
                )
                    continue;

                if (!typeItem.GetFields().Any(f => !f.Name.StartsWith("__", StringComparison.InvariantCulture)) && typeItem.GqlType != GqlTypes.Union && typeItem.BaseTypesReadOnly.Count == 0)
                    continue;

                types.AppendLine(OutputSchemaType(schema, typeItem));
            }

            return types.ToString();
        }

        private static string GetDirectives(IEnumerable<ISchemaDirective> directives)
        {
            return string.Join("", directives.Select(d => " " + d.ToGraphQLSchemaString()).Distinct());
        }

        private static string GetGqlArgs(ISchemaProvider schema, IField field, string noArgs = "")
        {
            if (field.Arguments == null || !field.Arguments.Any() || field.ArgumentsAreInternal)
                return noArgs;

            var all = field.Arguments.Select(f =>
            {
                var arg = schema.SchemaFieldNamer(f.Key) + ": " + f.Value.Type.GqlTypeForReturnOrArgument;

                var defaultValue = GetArgDefaultValue(f.Value.DefaultValue, schema.SchemaFieldNamer);
                if (!string.IsNullOrEmpty(defaultValue))
                {
                    arg += " = " + defaultValue;
                }

                return arg;
            });

            var args = string.Join(", ", all);
            return string.IsNullOrEmpty(args) ? string.Empty : $"({args})";
        }

        public static string? GetArgDefaultValue(object? value, Func<string, string> fieldNamer)
        {
            if (value == null || value == DBNull.Value)
            {
                return string.Empty;
            }

            var ret = string.Empty;
            var valueType = value.GetType();

            if (valueType == typeof(string))
            {
                return $"\"{(((string)value == string.Empty) ? string.Empty : value)}\"";
            }
            if (valueType == typeof(bool))
            {
                return value?.ToString()?.ToLower(CultureInfo.InvariantCulture);
            }
            else if (valueType.IsValueType)
            {
                return value?.ToString();
            }
            else if (value is IEnumerable e)
            {
                return $"[{string.Join(", ", e.Cast<object>().Select(item => GetArgDefaultValue(item, fieldNamer)).Where(item => item != null))}]";
            }
            else if (valueType.IsConstructedGenericType && valueType.GetGenericTypeDefinition() == typeof(EntityQueryType<>))
            {
                if (((BaseEntityQueryType)value).HasValue)
                {
                    var property = valueType.GetProperty("Query");
                    return $"\"{property!.GetValue(value)}\"";
                }
                return string.Empty;
            }
            else if (value is object o)
            {
                ret += "{ ";
                ret += string.Join(
                    ", ",
                    valueType
                        .GetProperties()
                        .Select(property =>
                        {
                            var propValue = property.GetValue(o);
                            var propertyValue = GetArgDefaultValue(propValue, fieldNamer);
                            if (string.IsNullOrEmpty(propertyValue))
                                return null;

                            return $"{fieldNamer(property.Name)}: {propertyValue}";
                        })
                        .Where(i => i != null)
                );
                ret += string.Join(
                    ", ",
                    valueType
                        .GetFields()
                        .Select(property =>
                        {
                            var propValue = property.GetValue(o);
                            var propertyValue = GetArgDefaultValue(propValue, fieldNamer);
                            if (string.IsNullOrEmpty(propertyValue))
                                return null;

                            return $"{fieldNamer(property.Name)}: {propertyValue}";
                        })
                        .Where(i => i != null)
                );
                ret += " }";
            }

            return ret;
        }

        private static string GetDirectiveArgs(ISchemaProvider schema, IDirectiveProcessor directive)
        {
            var args = directive.GetArguments(schema);
            if (args == null || !args.Any())
                return string.Empty;

            var allArgs = string.Join(", ", args.Select(f => f.Key + ": " + f.Value.Type.GqlTypeForReturnOrArgument));
            return string.IsNullOrEmpty(allArgs) ? string.Empty : $"({allArgs})";
        }

        private static string OutputSchemaType(ISchemaProvider schema, ISchemaType schemaType)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(schemaType.Description))
                sb.AppendLine($"\"\"\"{EscapeString(schemaType.Description)}\"\"\"");

            if (schemaType.GqlType == GqlTypes.Union)
            {
                if (schemaType.PossibleTypesReadOnly.Count == 0)
                {
                    return string.Empty;
                }

                sb.AppendLine($"union {schemaType.Name} = {string.Join(" | ", schemaType.PossibleTypesReadOnly.Select(i => i.Name))}");
                return sb.ToString();
            }

            var type = schemaType.GqlType switch
            {
                GqlTypes.InputObject => "input",
                GqlTypes.Interface => "interface",
                GqlTypes.Union => "union",
                _ => "type"
            };

            var implements = "";
            if (schemaType.BaseTypesReadOnly != null && schemaType.BaseTypesReadOnly.Count > 0)
            {
                implements += $" implements {string.Join(" & ", schemaType.BaseTypesReadOnly.Select(i => i.Name))}";
            }

            sb.AppendLine($"{type} {schemaType.Name}{implements}{GetDirectives(schemaType.Directives)} {{");

            foreach (var field in schemaType.GetFields().OrderBy(s => s.Name))
            {
                if (field.Name.StartsWith("__", StringComparison.InvariantCulture))
                    continue;
                if (!string.IsNullOrEmpty(field.Description))
                    sb.AppendLine($"\t\"\"\"{EscapeString(field.Description)}\"\"\"");
                sb.AppendLine($"\t{schema.SchemaFieldNamer(field.Name)}{GetGqlArgs(schema, field)}: {field.ReturnType.GqlTypeForReturnOrArgument}{GetDirectives(field.DirectivesReadOnly)}");
            }
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
#pragma warning restore CA1305
