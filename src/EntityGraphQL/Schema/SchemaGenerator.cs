using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EntityGraphQL.Directives;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.Directives;

namespace EntityGraphQL.Schema;

// can remove this when/if we drop netstandard2.1
#pragma warning disable CA1305

public class SchemaGenerator
{
    internal static string EscapeString(string? input)
    {
        if (input == null)
            return string.Empty;
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    /// <param name="requestContext">When supplied only the types and fields this user may access are output, using the same
    /// rules as introspection (see <see cref="SchemaVisibility"/>). Null outputs everything</param>
    internal static string Make(ISchemaProvider schema, bool includeDescriptions = true, QueryRequestContext? requestContext = null)
    {
        var visibility = SchemaVisibility.For(schema, requestContext);
        var rootQueryType = schema.GetSchemaType(schema.QueryContextType, false, null);
        var mutationType = schema.Mutation().SchemaType;
        var subscriptionType = schema.Subscription().SchemaType;

        var types = BuildSchemaTypes(schema, includeDescriptions, visibility);

        var schemaBuilder = new StringBuilder("schema {");
        schemaBuilder.AppendLine();
        schemaBuilder.AppendLine($"\tquery: {rootQueryType.Name}");
        bool outputMutation = VisibleFields(mutationType, visibility).Any();
        bool outputSubscription = VisibleFields(subscriptionType, visibility).Any();
        if (outputMutation)
            schemaBuilder.AppendLine($"\tmutation: {mutationType.Name}");
        if (outputSubscription)
            schemaBuilder.AppendLine($"\tsubscription: {subscriptionType.Name}");
        schemaBuilder.AppendLine("}");

        schemaBuilder.AppendLine();

        foreach (var item in schema.GetScalarTypes().Distinct().Where(visibility.IsTypeVisible).OrderBy(t => t.Name))
        {
            if (includeDescriptions && !string.IsNullOrEmpty(item.Description))
                schemaBuilder.AppendLine($"\"\"\"{EscapeString(item.Description)}\"\"\"");
            schemaBuilder.AppendLine($"scalar {item.Name}{GetDirectives(item.Directives)}");
        }
        schemaBuilder.AppendLine();

        foreach (var directive in schema.GetDirectives().OrderBy(t => t.Name))
        {
            if (includeDescriptions && !string.IsNullOrEmpty(directive.Description))
                schemaBuilder.AppendLine($"\"\"\"{EscapeString(directive.Description)}\"\"\"");

            schemaBuilder.AppendLine($"directive @{directive.Name}{GetDirectiveArgs(schema, directive)} on {string.Join(" | ", directive.Location.Select(i => i.GetDescription()))}");
        }
        schemaBuilder.AppendLine();

        schemaBuilder.Append(BuildEnumTypes(schema, includeDescriptions, visibility));

        schemaBuilder.AppendLine(OutputSchemaType(schema, schema.GetSchemaType(schema.QueryContextName, null), includeDescriptions, visibility));

        schemaBuilder.Append(types);

        if (outputMutation)
            schemaBuilder.AppendLine(OutputSchemaType(schema, schema.Mutation().SchemaType, includeDescriptions, visibility));
        if (outputSubscription)
            schemaBuilder.AppendLine(OutputSchemaType(schema, schema.Subscription().SchemaType, includeDescriptions, visibility));

        return schemaBuilder.ToString();
    }

    /// <summary>Fields of a type that are output: not internal (__) and visible to the user, if filtering</summary>
    private static IEnumerable<IField> VisibleFields(ISchemaType schemaType, SchemaVisibility visibility) =>
        schemaType.GetFields().Where(f => !f.Name.StartsWith("__", StringComparison.InvariantCulture) && visibility.IsFieldVisible(f));

    private static string BuildEnumTypes(ISchemaProvider schema, bool includeDescriptions, SchemaVisibility visibility)
    {
        var types = new StringBuilder();
        foreach (var typeItem in schema.GetNonContextTypes().OrderBy(t => t.Name))
        {
            if (typeItem.Name.StartsWith("__", StringComparison.InvariantCulture) || !typeItem.IsEnum)
                continue;
            if (!visibility.IsTypeVisible(typeItem))
                continue;

            if (includeDescriptions && !string.IsNullOrEmpty(typeItem.Description))
                types.AppendLine($"\"\"\"{EscapeString(typeItem.Description)}\"\"\"");

            types.AppendLine($"enum {typeItem.Name} {{");
            foreach (var field in typeItem.GetFields().OrderBy(t => t.Name))
            {
                if (field.Name.StartsWith("__", StringComparison.InvariantCulture))
                    continue;

                if (includeDescriptions && !string.IsNullOrEmpty(field.Description))
                    types.AppendLine($"\t\"\"\"{EscapeString(field.Description)}\"\"\"");

                types.AppendLine($"\t{field.Name}{GetDirectives(field.DirectivesReadOnly)}");
            }
            types.AppendLine("}");
            types.AppendLine();
        }

        return types.ToString();
    }

    private static string BuildSchemaTypes(ISchemaProvider schema, bool includeDescriptions, SchemaVisibility visibility)
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

            if (!visibility.IsTypeVisible(typeItem))
                continue;

            if (!typeItem.GetFields().Any(f => !f.Name.StartsWith("__", StringComparison.InvariantCulture)) && typeItem.GqlType != GqlTypes.Union && typeItem.BaseTypesReadOnly.Count == 0)
                continue;

            var output = OutputSchemaType(schema, typeItem, includeDescriptions, visibility);
            // without a request context keep the historic output byte-for-byte (it appended even an empty union)
            if (output.Length > 0 || !visibility.IsFiltering)
                types.AppendLine(output);
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

            if (f.Value.IsDeprecated)
            {
                arg += string.IsNullOrEmpty(f.Value.DeprecationReason) ? " @deprecated" : $" @deprecated(reason: \"{f.Value.DeprecationReason}\")";
            }

            return arg;
        });

        var args = string.Join(", ", all);
        return string.IsNullOrEmpty(args) ? string.Empty : $"({args})";
    }

    private static readonly JsonSerializerOptions jsonLiteralOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string? GetArgDefaultValue(DefaultArgValue defaultArgValue, Func<string, string> fieldNamer, bool nullAsValue = true)
    {
        if (!defaultArgValue.IsSet)
        {
            return string.Empty;
        }

        if (defaultArgValue.Value == null || defaultArgValue.Value == DBNull.Value)
        {
            return nullAsValue ? "null" : string.Empty;
        }

        var ret = string.Empty;
        var valueType = defaultArgValue.Value.GetType();

        if (valueType == typeof(bool))
        {
            return defaultArgValue.Value.ToString()?.ToLower(CultureInfo.InvariantCulture);
        }
        if (valueType.IsEnum)
        {
            return defaultArgValue.Value.ToString();
        }
        if (valueType == typeof(string) || valueType.IsValueType)
        {
            // JSON number and string literals are valid GraphQL literals: invariant numbers, escaped + quoted strings,
            // ISO formatted dates, quoted Guids. Structs serialize to JSON objects which are not, so fall through for those
            var json = JsonSerializer.Serialize(defaultArgValue.Value, valueType, jsonLiteralOptions);
            if (!json.StartsWith('{'))
                return json;
        }
        if (defaultArgValue.Value is IEnumerable e)
        {
            return $"[{string.Join(", ", e.Cast<object>().Select(item => GetArgDefaultValue(new DefaultArgValue(true, item), fieldNamer)).Where(item => item != null))}]";
        }
        else if (defaultArgValue.Value is object o)
        {
            ret += "{ ";
            ret += string.Join(
                ", ",
                valueType
                    .GetProperties()
                    .Select(property =>
                    {
                        var propValue = property.GetValue(o);
                        var propertyValue = GetArgDefaultValue(new DefaultArgValue(true, propValue), fieldNamer, false);
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
                        var propertyValue = GetArgDefaultValue(new DefaultArgValue(true, propValue), fieldNamer, false);
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

    private static string OutputSchemaType(ISchemaProvider schema, ISchemaType schemaType, bool includeDescriptions, SchemaVisibility visibility)
    {
        var sb = new StringBuilder();
        var fields = VisibleFields(schemaType, visibility).OrderBy(s => s.Name).ToList();

        if (includeDescriptions && !string.IsNullOrEmpty(schemaType.Description))
            sb.AppendLine($"\"\"\"{EscapeString(schemaType.Description)}\"\"\"");

        if (schemaType.GqlType == GqlTypes.Union)
        {
            var possibleTypes = schemaType.PossibleTypesReadOnly.Where(visibility.IsTypeVisible).ToList();
            if (possibleTypes.Count == 0)
            {
                return string.Empty;
            }

            sb.AppendLine($"union {schemaType.Name} = {string.Join(" | ", possibleTypes.Select(i => i.Name))}");
            return sb.ToString();
        }

        var type = schemaType.GqlType switch
        {
            GqlTypes.InputObject => "input",
            GqlTypes.Interface => "interface",
            GqlTypes.Union => "union",
            _ => "type",
        };

        var implements = "";
        var baseTypes = schemaType.BaseTypesReadOnly?.Where(visibility.IsTypeVisible).ToList();
        if (baseTypes != null && baseTypes.Count > 0)
        {
            implements += $" implements {string.Join(" & ", baseTypes.Select(i => i.Name))}";
        }

        // the query root can be left with no fields (SchemaVisibility hides any other type it empties). The SDL grammar
        // allows a type with no field block, so it stays a parseable root rather than disappearing from under `schema`
        if (visibility.IsFiltering && fields.Count == 0 && schemaType.Name == schema.QueryContextName)
        {
            sb.AppendLine($"{type} {schemaType.Name}{implements}{GetDirectives(schemaType.Directives)}");
            return sb.ToString();
        }

        sb.AppendLine($"{type} {schemaType.Name}{implements}{GetDirectives(schemaType.Directives)} {{");

        foreach (var field in fields)
        {
            if (includeDescriptions && !string.IsNullOrEmpty(field.Description))
                sb.AppendLine($"\t\"\"\"{EscapeString(field.Description)}\"\"\"");
            sb.AppendLine($"\t{schema.SchemaFieldNamer(field.Name)}{GetGqlArgs(schema, field)}: {field.ReturnType.GqlTypeForReturnOrArgument}{GetDirectives(field.DirectivesReadOnly)}");
        }
        sb.AppendLine("}");

        return sb.ToString();
    }
}
#pragma warning restore CA1305
