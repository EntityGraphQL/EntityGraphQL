using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Directives;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

public static class GraphQLParser
{
    private static readonly Dictionary<string, object?> EmptyArguments = new();
    private static readonly Dictionary<string, ArgType> EmptyVariableDefinitions = new();

    public static GraphQLDocument Parse(QueryRequest request, ISchemaProvider schemaProvide)
    {
        return Parse(request.Query, schemaProvide, request.Variables ?? new QueryVariables());
    }

    public static GraphQLDocument Parse(string? query, ISchemaProvider schemaProvider)
    {
        return Parse(query, schemaProvider, new QueryVariables());
    }

    public static GraphQLDocument Parse(string? query, ISchemaProvider schemaProvider, QueryVariables queryVariables)
    {
        if (query == null)
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"GraphQL Query can not be null");

        query ??= string.Empty;
        var parseContext = new GraphQLParseContext(query, queryVariables);
        var reader = new SpanReader(query);

        var document = new GraphQLDocument(schemaProvider);
        reader.SkipIgnored();
        while (!reader.End)
        {
            ParseDefinition(parseContext, document, ref reader);
            reader.SkipIgnored();
        }

        ValidateFragmentCycles(document);
        return document;
    }

    private static void ParseDefinition(GraphQLParseContext parseContext, GraphQLDocument document, ref SpanReader reader)
    {
        reader.SkipIgnored();
        if (reader.End)
            throw CreateParseException(parseContext, "Unexpected end of document.", reader.Position);

        SkipDescription(parseContext, ref reader);

        if (reader.TryConsumeKeyword("fragment"))
            ParseFragmentDefinition(parseContext, document, ref reader, document);
        else if (reader.TryPeek('{'))
        {
            var operation = CreateOperation(parseContext, "query", null, EmptyVariableDefinitions, document, ref reader);
            parseContext.CurrentOperation = operation;
            ParseSelectionSet(parseContext, operation, ref reader);
            parseContext.CurrentOperation = null;
            document.Operations.Add(operation);
        }
        else if (reader.TryConsumeKeyword("query"))
            document.Operations.Add(ParseOperationDefinition(parseContext, document, ref reader, "query"));
        else if (reader.TryConsumeKeyword("mutation"))
            document.Operations.Add(ParseOperationDefinition(parseContext, document, ref reader, "mutation"));
        else if (reader.TryConsumeKeyword("subscription"))
            document.Operations.Add(ParseOperationDefinition(parseContext, document, ref reader, "subscription"));
        else
        {
            var unexpected = reader.Peek();
            var token = unexpected == '\0' ? "EOF" : unexpected.ToString();
            throw CreateParseException(parseContext, $"Unexpected token '{token}' while parsing document.", reader.Position);
        }
    }

    private static void SkipDescription(GraphQLParseContext parseContext, ref SpanReader reader)
    {
        if (reader.TryPeek('"'))
        {
            // could store the description if we have a use for it one day
            ParseStringValue(parseContext, ref reader);
            reader.SkipIgnored();
        }
    }

    private static ExecutableGraphQLStatement ParseOperationDefinition(GraphQLParseContext parseContext, IGraphQLNode node, ref SpanReader reader, string operationType)
    {
        string? operationName = null;
        reader.SkipIgnored();
        if (!reader.End && IsNameStart(reader.Peek()))
        {
            operationName = ReadName(parseContext, ref reader, skipIgnored: false);
        }

        reader.SkipIgnored();
        Dictionary<string, ArgType>? variables = null;
        if (reader.TryConsume('('))
        {
            variables = ParseVariableDefinitions(parseContext, ref reader, operationName, node);
        }

        var operation = CreateOperation(parseContext, operationType, operationName, variables ?? EmptyVariableDefinitions, node, ref reader);
        parseContext.CurrentOperation = operation;
        ParseSelectionSet(parseContext, operation, ref reader);
        parseContext.CurrentOperation = null;

        return operation;
    }

    private static void ParseFragmentDefinition(GraphQLParseContext parseContext, GraphQLDocument document, ref SpanReader reader, IGraphQLNode node)
    {
        reader.SkipIgnored();
        var fragmentName = ReadName(parseContext, ref reader);
        reader.SkipIgnored();

        if (!reader.TryConsumeKeyword("on"))
            throw CreateParseException(parseContext, "Expected 'on' in fragment definition.", reader.Position);

        reader.SkipIgnored();
        var typeName = ReadName(parseContext, ref reader);
        var directives = ParseDirectives(parseContext, ref reader, ExecutableDirectiveLocation.FragmentDefinition, node);

        var schemaType =
            document.Schema.GetSchemaType(typeName, null) ?? throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Unknown type '{typeName}' in fragment '{fragmentName}'");
        var fragParameter = Expression.Parameter(schemaType.TypeDotnet, $"frag_{typeName}");
        var fragStatement = new GraphQLFragmentStatement(document.Schema, fragmentName, fragParameter, fragParameter);

        parseContext.InFragment = true;

        ParseSelectionSet(parseContext, fragStatement, ref reader);

        if (directives?.Count > 0)
        {
            foreach (var directive in directives)
            {
                // TODO args all of these
                directive.VisitNode(ExecutableDirectiveLocation.FragmentDefinition, document.Schema, fragStatement, new Dictionary<string, object?>(), null, null);
            }
        }

        parseContext.InFragment = false;

        document.Fragments.Add(fragmentName, fragStatement);
    }

    private static Dictionary<string, ArgType> ParseVariableDefinitions(GraphQLParseContext parseContext, ref SpanReader reader, string? operationName, IGraphQLNode node)
    {
        var variables = new Dictionary<string, ArgType>();
        reader.SkipIgnored();

        SkipDescription(parseContext, ref reader);

        while (true)
        {
            if (reader.TryConsume(')'))
                break;

            if (!reader.TryConsume('$'))
                throw CreateParseException(parseContext, "Expected '$' to start variable definition.", reader.Position);

            var name = ReadName(parseContext, ref reader, skipIgnored: false);
            reader.SkipIgnored();
            reader.Expect(':', parseContext, "Expected ':' after variable name.");

            var type = ParseVariableType(parseContext, ref reader);
            reader.SkipIgnored();

            object? defaultValue = null;
            if (reader.TryConsume('='))
            {
                defaultValue = ParseValue(parseContext, ref reader);
                reader.SkipIgnored();
            }

            var directives = ParseDirectives(parseContext, ref reader, ExecutableDirectiveLocation.VariableDefinition, node);
            var isRequired = type.OuterNonNull;
            if (isRequired && !parseContext.QueryVariables.ContainsKey(name))
            {
                throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Missing required variable '{name}' on operation '{operationName}'");
            }

            var schemaType = node.Schema.GetSchemaType(type.TypeName, null);
            var dotnetType = schemaType.TypeDotnet;

            if (type.IsList)
            {
                dotnetType = typeof(List<>).MakeGenericType(dotnetType);
            }

            if (!isRequired && dotnetType.IsValueType)
            {
                dotnetType = typeof(Nullable<>).MakeGenericType(dotnetType);
            }

            var gqlTypeInfo = new GqlTypeInfo(() => schemaType, dotnetType)
            {
                TypeNotNullable = isRequired,
                ElementTypeNullable = type.IsList && !type.InnerNonNull,
                IsList = type.IsList,
            };

            var argType = new ArgType(type.TypeName, dotnetType.Name, gqlTypeInfo, dotnetType) { DefaultValue = new DefaultArgValue(defaultValue != null, defaultValue), IsRequired = isRequired };

            if (directives?.Count > 0)
            {
                foreach (var directive in directives)
                {
                    directive.VisitNode(ExecutableDirectiveLocation.VariableDefinition, node.Schema, null, new Dictionary<string, object?>(), null, null);
                }
            }

            variables.Add(name, argType);
            reader.SkipIgnored();
        }

        return variables;
    }

    private static GraphQLVariableType ParseVariableType(GraphQLParseContext parseContext, ref SpanReader reader)
    {
        reader.SkipIgnored();

        if (reader.TryConsume('['))
        {
            var typeName = ReadName(parseContext, ref reader);
            reader.SkipIgnored();
            var innerNonNull = reader.TryConsume('!');
            reader.SkipIgnored();
            reader.Expect(']', parseContext, "Expected ']' to close list type.");
            reader.SkipIgnored();
            var outerNonNull = reader.TryConsume('!');
            return new GraphQLVariableType(typeName, true, innerNonNull, outerNonNull);
        }

        var namedType = ReadName(parseContext, ref reader);
        reader.SkipIgnored();
        var nonNull = reader.TryConsume('!');
        return new GraphQLVariableType(namedType, false, false, nonNull);
    }

    private static void ParseSelectionSet(GraphQLParseContext parseContext, IGraphQLNode node, ref SpanReader reader)
    {
        reader.SkipIgnored();
        reader.Expect('{', parseContext, "Expected '{' to start selection set.");

        reader.SkipIgnored();

        while (!reader.TryPeek('}'))
        {
            var field = ParseSelection(parseContext, node, ref reader);
            node.AddField(field);
            reader.SkipIgnored();

            if (field.Field?.ReturnType.SchemaType.RequiresSelection == true)
            {
                if (
                    (field is GraphQLMutationField mutField && mutField.ResultSelection == null)
                    || (field is GraphQLSubscriptionField subField && subField.ResultSelection == null)
                    || (field is not GraphQLMutationField && field is not GraphQLSubscriptionField && field.QueryFields.Count == 0)
                )
                    throw new EntityGraphQLException($"Field '{field.Name}' requires a selection set defining the fields you would like to select.");
            }
        }

        reader.Expect('}', parseContext, "Expected '}' to close selection set.");
    }

    private static BaseGraphQLField ParseSelection(GraphQLParseContext parseContext, IGraphQLNode node, ref SpanReader reader)
    {
        reader.SkipIgnored();

        if (TryConsumeSpread(ref reader))
        {
            reader.SkipIgnored();
            if (reader.TryConsumeKeyword("on"))
            {
                reader.SkipIgnored();
                var typeName = ReadName(parseContext, ref reader);
                var directives = ParseDirectives(parseContext, ref reader, ExecutableDirectiveLocation.InlineFragment, node);

                var schemaType = node.Schema.GetSchemaType(typeName, null) ?? throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Unknown type '{typeName}' in inline fragment");
                var fragParameter = Expression.Parameter(schemaType.TypeDotnet, $"frag_{typeName}");
                var inlineFragField = new GraphQLInlineFragmentField(node.Schema, typeName, fragParameter, fragParameter, node.ParentNode!);
                ParseSelectionSet(parseContext, inlineFragField, ref reader);

                if (directives != null && directives.Count > 0)
                {
                    inlineFragField.AddDirectives(directives);
                    foreach (var directive in directives)
                    {
                        directive.VisitNode(ExecutableDirectiveLocation.InlineFragment, node.Schema, inlineFragField, new Dictionary<string, object?>(), null, null);
                    }
                }

                return inlineFragField;
            }
            else
            {
                var fragmentName = ReadName(parseContext, ref reader);
                var directives = ParseDirectives(parseContext, ref reader, ExecutableDirectiveLocation.FragmentSpread, node);
                var fragField = new GraphQLFragmentSpreadField(node.Schema, fragmentName, null, node.RootParameter!, node.ParentNode!);
                if (directives != null && directives.Count > 0)
                {
                    fragField.AddDirectives(directives);
                }
                return fragField;
            }
        }

        return ParseField(parseContext, node, ref reader);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryConsumeSpread(ref SpanReader reader)
    {
        if (reader.Matches("..."))
        {
            reader.Advance(3);
            return true;
        }

        return false;
    }

    private static BaseGraphQLField ParseField(GraphQLParseContext parseContext, IGraphQLNode node, ref SpanReader reader)
    {
        SkipDescription(parseContext, ref reader);

        var aliasOrName = ReadName(parseContext, ref reader);
        reader.SkipIgnored();

        string? alias = null;
        var fieldName = aliasOrName;

        if (reader.TryConsume(':'))
        {
            reader.SkipIgnored();
            fieldName = ReadName(parseContext, ref reader);
            alias = aliasOrName;
        }

        var arguments = ParseArguments(parseContext, ref reader);
        var directives = ParseDirectives(parseContext, ref reader, ExecutableDirectiveLocation.Field, node);
        var field = CreateField(parseContext, fieldName, alias, arguments, directives, node);

        if (reader.TryPeek('{'))
        {
            // For subscription/mutation fields, we need to create and populate the ResultSelection
            if (field is GraphQLSubscriptionField subscriptionField)
            {
                var actualField = subscriptionField.Field!;
                var nextContextParam = (ParameterExpression)subscriptionField.NextFieldContext!;
                BaseGraphQLQueryField resultSelection;

                if (actualField.ReturnType.IsList)
                {
                    var elementType = actualField.ReturnType.SchemaType.TypeDotnet;
                    var elementParam = Expression.Parameter(elementType, $"p_{elementType.Name}");
                    resultSelection = new GraphQLListSelectionField(
                        node.Schema,
                        actualField,
                        aliasOrName,
                        elementParam,
                        field.RootParameter!,
                        nextContextParam,
                        node,
                        subscriptionField.Arguments as Dictionary<string, object?>
                    );
                }
                else
                {
                    resultSelection = new GraphQLObjectProjectionField(
                        node.Schema,
                        actualField,
                        aliasOrName,
                        nextContextParam,
                        field.RootParameter!,
                        subscriptionField,
                        subscriptionField.Arguments as Dictionary<string, object?>
                    );
                }

                ParseSelectionSet(parseContext, resultSelection, ref reader);
                subscriptionField.ResultSelection = resultSelection;
            }
            else if (field is GraphQLMutationField mutationField)
            {
                var actualField = mutationField.Field!;
                var nextContextParam = (ParameterExpression)mutationField.NextFieldContext!;
                BaseGraphQLQueryField resultSelection;

                if (actualField.ReturnType.IsList)
                {
                    var elementType = actualField.ReturnType.SchemaType.TypeDotnet;
                    var elementParam = Expression.Parameter(elementType, $"p_{elementType.Name}");
                    resultSelection = new GraphQLListSelectionField(
                        node.Schema,
                        actualField,
                        aliasOrName,
                        elementParam,
                        field.RootParameter!,
                        nextContextParam,
                        node,
                        mutationField.Arguments as Dictionary<string, object?>
                    );
                }
                else
                {
                    resultSelection = new GraphQLObjectProjectionField(
                        node.Schema,
                        actualField,
                        aliasOrName,
                        nextContextParam,
                        field.RootParameter!,
                        mutationField,
                        mutationField.Arguments as Dictionary<string, object?>
                    );
                }

                ParseSelectionSet(parseContext, resultSelection, ref reader);
                mutationField.ResultSelection = resultSelection;
            }
            else
            {
                ParseSelectionSet(parseContext, field, ref reader);
            }
        }

        return field;
    }

    private static Dictionary<string, object?> ParseArguments(GraphQLParseContext parseContext, ref SpanReader reader)
    {
        reader.SkipIgnored();
        if (!reader.TryConsume('('))
            return EmptyArguments;

        Dictionary<string, object?>? arguments = null;
        reader.SkipIgnored();

        while (!reader.TryPeek(')'))
        {
            var argName = ReadName(parseContext, ref reader);
            reader.SkipIgnored();
            reader.Expect(':', parseContext, $"Expected ':' after argument name '{argName}'.");
            reader.SkipIgnored();
            var value = ParseValue(parseContext, ref reader);
            (arguments ??= new Dictionary<string, object?>())[argName] = value;
            reader.SkipIgnored();
        }

        reader.Expect(')', parseContext, "Expected ')' to close argument list.");
        return arguments ?? EmptyArguments;
    }

    private static List<GraphQLDirective>? ParseDirectives(GraphQLParseContext parseContext, ref SpanReader reader, ExecutableDirectiveLocation location, IGraphQLNode node)
    {
        reader.SkipIgnored();
        List<GraphQLDirective>? directives = null;

        while (reader.TryConsume('@'))
        {
            var name = ReadName(parseContext, ref reader, skipIgnored: false);
            var arguments = ParseArguments(parseContext, ref reader);
            directives ??= new List<GraphQLDirective>();
            var processor = node.Schema.GetDirective(name);
            if (!processor.Location.Contains(location))
                throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Directive '{name}' can not be used on '{location}'");

            var processedArgs = arguments;
            if (processedArgs.Count > 0)
            {
                foreach (var arg in processedArgs)
                {
                    if (arg.Value is Expression)
                    {
                        processedArgs[arg.Key] = arg.Value;
                    }
                }
            }

            directives.Add(new GraphQLDirective(name, processor, processedArgs));
            reader.SkipIgnored();
        }

        return directives;
    }

    private static object? ParseValue(GraphQLParseContext parseContext, ref SpanReader reader)
    {
        reader.SkipIgnored();
        if (reader.End)
            throw CreateParseException(parseContext, "Unexpected end of document while reading value.", reader.Position);

        var ch = reader.Peek();
        switch (ch)
        {
            case '$':
                reader.Advance();
                if (parseContext.CurrentOperation == null && !parseContext.InFragment)
                    throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, "Variable used but no current operation found");
                var variableName = ReadName(parseContext, ref reader, skipIgnored: false);
                // If we're in a fragment, we can't resolve the variable yet since fragments don't have operation context
                // We'll resolve it later when the fragment is expanded into an operation
                if (parseContext.InFragment)
                    return new VariableReference(variableName);
                if (parseContext.CurrentOperation == null)
                    throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, "Variable used but no current operation found");
                var variableExpression = Expression.PropertyOrField(parseContext.CurrentOperation.OpVariableParameter!, variableName);
                return variableExpression;
            case '"':
                return ParseStringValue(parseContext, ref reader);
            case '[':
                return ParseListValue(parseContext, ref reader);
            case '{':
                return ParseObjectValue(parseContext, ref reader);
            case '-':
                return ParseNumberValue(parseContext, ref reader);
            default:
                if (char.IsDigit(ch))
                    return ParseNumberValue(parseContext, ref reader);
                if (reader.TryConsumeKeyword("true"))
                    return true;
                if (reader.TryConsumeKeyword("false"))
                    return false;
                if (reader.TryConsumeKeyword("null"))
                    return null;
                if (IsNameStart(ch))
                    return ReadName(parseContext, ref reader, skipIgnored: false);

                throw CreateParseException(parseContext, $"Unexpected token '{ch}' while parsing value.", reader.Position);
        }
    }

    private static List<object?> ParseListValue(GraphQLParseContext parseContext, ref SpanReader reader)
    {
        reader.SkipIgnored();
        reader.Expect('[', parseContext, "Expected '[' to start list value.");
        var list = new List<object?>();
        reader.SkipIgnored();

        while (!reader.TryPeek(']'))
        {
            var value = ParseValue(parseContext, ref reader);
            list.Add(value);
            reader.SkipIgnored();
        }

        reader.Expect(']', parseContext, "Expected ']' to close list value.");
        return list;
    }

    private static Dictionary<string, object?> ParseObjectValue(GraphQLParseContext parseContext, ref SpanReader reader)
    {
        reader.SkipIgnored();
        reader.Expect('{', parseContext, "Expected '{' to start object value.");

        var obj = new Dictionary<string, object?>();
        reader.SkipIgnored();

        while (!reader.TryPeek('}'))
        {
            var fieldName = ReadName(parseContext, ref reader);
            reader.SkipIgnored();
            reader.Expect(':', parseContext, $"Expected ':' after object field '{fieldName}'.");
            reader.SkipIgnored();
            var value = ParseValue(parseContext, ref reader);
            obj[fieldName] = value;
            reader.SkipIgnored();
        }

        reader.Expect('}', parseContext, "Expected '}' to close object value.");
        return obj;
    }

    private static object ParseNumberValue(GraphQLParseContext parseContext, ref SpanReader reader)
    {
        var start = reader.Position;

        if (reader.TryConsume('-'))
        {
            if (reader.End || !char.IsDigit(reader.Peek()))
                throw CreateParseException(parseContext, "Invalid number literal.", reader.Position);
        }

        if (!reader.End && reader.Peek() == '0')
        {
            reader.Advance();
            if (!reader.End && char.IsDigit(reader.Peek()))
                throw CreateParseException(parseContext, "Invalid number literal with leading zero.", reader.Position);
        }
        else
        {
            while (!reader.End && char.IsDigit(reader.Peek()))
                reader.Advance();
        }

        var isFloat = false;

        if (!reader.End && reader.Peek() == '.')
        {
            isFloat = true;
            reader.Advance();
            if (reader.End || !char.IsDigit(reader.Peek()))
                throw CreateParseException(parseContext, "Invalid float literal.", reader.Position);
            while (!reader.End && char.IsDigit(reader.Peek()))
                reader.Advance();
        }

        if (!reader.End)
        {
            var next = reader.Peek();
            if (next == 'e' || next == 'E')
            {
                isFloat = true;
                reader.Advance();
                if (!reader.End && (reader.Peek() == '+' || reader.Peek() == '-'))
                    reader.Advance();
                if (reader.End || !char.IsDigit(reader.Peek()))
                    throw CreateParseException(parseContext, "Invalid float literal exponent.", reader.Position);
                while (!reader.End && char.IsDigit(reader.Peek()))
                    reader.Advance();
            }
        }

        var span = reader.Slice(start, reader.Position - start);
        if (!isFloat && long.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var longValue))
            return longValue;

        var decimalValue = decimal.Parse(span, NumberStyles.Float, CultureInfo.InvariantCulture);
        return decimalValue;
    }

    private static string ParseStringValue(GraphQLParseContext parseContext, ref SpanReader reader)
    {
        reader.SkipIgnored();
        if (reader.Matches("\"\"\""))
        {
            reader.Advance(3);
            return ParseBlockStringValue(parseContext, ref reader);
        }

        reader.Expect('"', parseContext, "Expected '\"' to start string literal.");
        var start = reader.Position;

        while (!reader.End)
        {
            var ch = reader.Peek();
            if (ch == '"')
            {
                var result = reader.GetString(start, reader.Position - start);
                reader.Advance();
                return result;
            }

            if (ch == '\\')
            {
                // Found an escape sequence, fall back to StringBuilder path
                return ParseEscapedStringValue(parseContext, ref reader, start);
            }

            if (ch < 0x20)
                throw CreateParseException(parseContext, "Invalid control character in string literal.", reader.Position);

            reader.Advance();
        }

        throw CreateParseException(parseContext, "Unterminated string literal.", reader.Position);
    }

    private static string ParseEscapedStringValue(GraphQLParseContext parseContext, ref SpanReader reader, int start)
    {
        var sb = new StringBuilder();
        sb.Append(reader.Slice(start, reader.Position - start));

        while (!reader.End)
        {
            var ch = reader.Peek();
            if (ch == '"')
            {
                reader.Advance();
                return sb.ToString();
            }

            if (ch == '\\')
            {
                reader.Advance(); // consume '\'
                if (reader.End)
                    throw CreateParseException(parseContext, "Unterminated escape sequence in string literal.", reader.Position);

                var escape = reader.Peek();
                reader.Advance(); // consume escape char
                sb.Append(ParseEscapedSequence(parseContext, ref reader, escape));
                continue;
            }

            if (ch < 0x20)
                throw CreateParseException(parseContext, "Invalid control character in string literal.", reader.Position);

            sb.Append(ch);
            reader.Advance();
        }

        throw CreateParseException(parseContext, "Unterminated string literal.", reader.Position);
    }

    private static string ParseEscapedSequence(GraphQLParseContext parseContext, ref SpanReader reader, char escape)
    {
        return escape switch
        {
            '"' => "\"",
            '/' => "/",
            '\\' => "\\",
            'b' => "\b",
            'f' => "\f",
            'n' => "\n",
            'r' => "\r",
            't' => "\t",
            'u' => ParseUnicodeEscape(parseContext, ref reader),
            _ => throw CreateParseException(parseContext, $"Invalid escape sequence '\\{escape}'.", reader.Position),
        };
    }

    private static string ParseUnicodeEscape(GraphQLParseContext parseContext, ref SpanReader reader)
    {
        // Check for variable-width unicode escape \u{...} (GraphQL Sept 2025 spec)
        if (!reader.End && reader.Peek() == '{')
        {
            reader.Advance(); // consume '{'

            var value = 0;
            var digitCount = 0;

            while (!reader.End && reader.Peek() != '}')
            {
                var ch = reader.Peek();
                if (!IsHexDigit(ch))
                    throw CreateParseException(parseContext, "Invalid hex digit in unicode escape sequence.", reader.Position);

                reader.Advance();
                value = (value << 4) + HexValue(ch);
                digitCount++;

                // Unicode code points are up to 6 hex digits (0x10FFFF)
                if (digitCount > 6)
                    throw CreateParseException(parseContext, "Unicode escape sequence too long.", reader.Position);
            }

            if (reader.End)
                throw CreateParseException(parseContext, "Unterminated unicode escape sequence.", reader.Position);

            if (digitCount == 0)
                throw CreateParseException(parseContext, "Empty unicode escape sequence.", reader.Position);

            reader.Expect('}', parseContext, "Expected '}' to close unicode escape sequence.");

            // Validate code point range (0x0000 to 0x10FFFF)
            if (value > 0x10FFFF)
                throw CreateParseException(parseContext, "Unicode code point out of range.", reader.Position);

            // Validate not a surrogate (0xD800-0xDFFF are reserved for UTF-16 surrogates)
            if (value >= 0xD800 && value <= 0xDFFF)
                throw CreateParseException(parseContext, "Unicode escape sequence cannot specify surrogate code points.", reader.Position);

            return char.ConvertFromUtf32(value);
        }

        // Fixed-width 4-digit unicode escape \uXXXX (original format)
        if (reader.Remaining < 4)
            throw CreateParseException(parseContext, "Incomplete unicode escape sequence.", reader.Position);

        var fixedValue = 0;
        for (var i = 0; i < 4; i++)
        {
            var ch = reader.Peek();
            reader.Advance();
            if (!IsHexDigit(ch))
                throw CreateParseException(parseContext, "Invalid unicode escape sequence.", reader.Position);

            fixedValue = (fixedValue << 4) + HexValue(ch);
        }

        // For 4-digit escapes, allow surrogates as they may be part of a surrogate pair
        // The spec allows legacy surrogate pair format: \uD83D\uDCA9
        // If it's a surrogate, return it as a char (will be combined with pair in string)
        if (fixedValue >= 0xD800 && fixedValue <= 0xDFFF)
        {
            // Return the surrogate as-is, it will be paired up in the string
            return ((char)fixedValue).ToString();
        }

        // Valid standalone code point
        try
        {
            return char.ConvertFromUtf32(fixedValue);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw CreateParseException(parseContext, $"Invalid unicode code point: 0x{fixedValue:X4}.", reader.Position);
        }
    }

    private static string ParseBlockStringValue(GraphQLParseContext parseContext, ref SpanReader reader)
    {
        var sb = (StringBuilder?)null;
        var start = reader.Position;

        while (!reader.End)
        {
            if (reader.Matches("\"\"\"") && !reader.HasOddNumberOfPrecedingBackslashes())
            {
                var length = reader.Position - start;
                string raw;
                if (sb == null)
                {
                    raw = reader.GetString(start, length);
                }
                else
                {
                    if (length > 0)
                        sb.Append(reader.Slice(start, length));
                    raw = sb.ToString();
                }

                reader.Advance(3);
                return NormalizeBlockString(raw);
            }

            var ch = reader.Peek();
            if (ch == '\\')
            {
                if (sb == null)
                    sb = new StringBuilder(reader.Position - start + 16);
                if (reader.Position > start)
                    sb.Append(reader.Slice(start, reader.Position - start));

                reader.Advance();
                if (reader.End)
                    throw CreateParseException(parseContext, "Unterminated escape sequence in block string literal.", reader.Position);

                var escape = reader.Peek();
                reader.Advance();
                sb.Append(ParseEscapedSequence(parseContext, ref reader, escape));
                start = reader.Position;
                continue;
            }

            reader.Advance();
        }

        throw CreateParseException(parseContext, "Unterminated block string literal.", reader.Position);
    }

    private static ReadOnlySpan<char> TrimCarriageReturn(ReadOnlySpan<char> value)
    {
        if (value.Length > 0 && value[^1] == '\r')
            return value[..^1];
        return value;
    }

    private static bool IsWhitespaceLine(ReadOnlySpan<char> source, (int Start, int Length) line)
    {
        var slice = TrimCarriageReturn(source.Slice(line.Start, line.Length));
        for (int i = 0; i < slice.Length; i++)
        {
            if (!char.IsWhiteSpace(slice[i]))
                return false;
        }
        return true;
    }

    private static string NormalizeBlockString(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        ReadOnlySpan<char> span = raw.AsSpan();
        int estimatedLineCount = 1;
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == '\n')
                estimatedLineCount++;
        }

        var lines = new List<(int Start, int Length)>(estimatedLineCount);

        int position = 0;
        while (position < span.Length)
        {
            int relativeIndex = span[position..].IndexOf('\n');
            if (relativeIndex >= 0)
            {
                lines.Add((position, relativeIndex));
                position += relativeIndex + 1;
            }
            else
            {
                lines.Add((position, span.Length - position));
                position = span.Length;
            }
        }

        if (raw.Length > 0 && raw[^1] == '\n')
        {
            lines.Add((span.Length, 0));
        }

        int startLine = 0;
        while (startLine < lines.Count && IsWhitespaceLine(span, lines[startLine]))
            startLine++;

        int endLine = lines.Count - 1;
        while (endLine >= startLine && IsWhitespaceLine(span, lines[endLine]))
            endLine--;

        if (startLine > endLine)
            return string.Empty;

        int commonIndent = int.MaxValue;
        for (int i = startLine; i <= endLine; i++)
        {
            var line = TrimCarriageReturn(span.Slice(lines[i].Start, lines[i].Length));
            if (line.Length == 0)
                continue;

            int indent = 0;
            while (indent < line.Length && (line[indent] == ' ' || line[indent] == '\t'))
            {
                indent++;
            }

            if (indent < line.Length)
                commonIndent = Math.Min(commonIndent, indent);
        }

        if (commonIndent == int.MaxValue)
            commonIndent = 0;

        var builder = new StringBuilder(span.Length);
        for (int i = startLine; i <= endLine; i++)
        {
            var line = TrimCarriageReturn(span.Slice(lines[i].Start, lines[i].Length));
            if (line.Length > commonIndent)
            {
                builder.Append(line[commonIndent..]);
            }

            if (i < endLine)
                builder.Append('\n');
        }

        return builder.ToString();
    }

    private static string ReadName(GraphQLParseContext parseContext, ref SpanReader reader, bool skipIgnored = true)
    {
        if (skipIgnored)
            reader.SkipIgnored();

        if (reader.End)
            throw CreateParseException(parseContext, "Unexpected end of document while reading name.", reader.Position);

        var ch = reader.Peek();
        if (!IsNameStart(ch))
            throw CreateParseException(parseContext, $"Invalid name start character '{ch}'.", reader.Position);

        var start = reader.Position;
        reader.Advance();

        while (!reader.End && IsNameContinue(reader.Peek()))
            reader.Advance();

        return reader.GetString(start, reader.Position - start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static EntityGraphQLException CreateParseException(GraphQLParseContext parseContext, string message, int position)
    {
        var (line, column) = GetLineAndColumn(parseContext.Source, position);
        return new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"{message} (line {line}, column {column})");
    }

    private static (int line, int column) GetLineAndColumn(ReadOnlySpan<char> source, int position)
    {
        var line = 1;
        var column = 1;
        var length = Math.Min(position, source.Length);
        var span = source[0..length];

        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return (line, column);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNameStart(char ch) => ch == '_' || char.IsLetter(ch);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNameContinue(char ch) => ch == '_' || char.IsLetterOrDigit(ch);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHexDigit(char ch) => (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f');

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HexValue(char ch) =>
        ch switch
        {
            >= '0' and <= '9' => ch - '0',
            >= 'A' and <= 'F' => ch - 'A' + 10,
            >= 'a' and <= 'f' => ch - 'a' + 10,
            _ => 0,
        };

    private ref struct SpanReader
    {
        private readonly ReadOnlySpan<char> buffer;
        private int position;

        public SpanReader(ReadOnlySpan<char> source)
        {
            buffer = source;
            position = 0;
        }

        public readonly bool End => position >= buffer.Length;
        public readonly int Position => position;
        public readonly int Remaining => buffer.Length - position;

        public void SkipIgnored()
        {
            while (!End)
            {
                var ch = buffer[position];
                if (ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r' || ch == ',' || ch == '\ufeff')
                {
                    position++;
                    continue;
                }

                if (ch == '#')
                {
                    position++;
                    while (!End)
                    {
                        ch = buffer[position];
                        if (ch == '\n' || ch == '\r')
                            break;
                        position++;
                    }

                    continue;
                }

                break;
            }
        }

        public bool TryConsume(char ch)
        {
            if (!End && buffer[position] == ch)
            {
                position++;
                return true;
            }

            return false;
        }

        public readonly bool TryPeek(char ch) => !End && buffer[position] == ch;

        public readonly char Peek() => End ? '\0' : buffer[position];

        public void Advance(int count = 1) => position += count;

        public readonly bool Matches(string value)
        {
            if (position + value.Length > buffer.Length)
                return false;

            return buffer.Slice(position, value.Length).SequenceEqual(value);
        }

        public bool TryConsumeKeyword(string keyword)
        {
            if (!Matches(keyword))
                return false;

            var nextIndex = position + keyword.Length;
            if (nextIndex < buffer.Length && IsNameContinue(buffer[nextIndex]))
                return false;

            position = nextIndex;
            return true;
        }

        public readonly ReadOnlySpan<char> Slice(int start, int length) => buffer.Slice(start, length);

        public readonly string GetString(int start, int length)
        {
            if (length <= 0)
                return string.Empty;

            return new string(buffer.Slice(start, length));
        }

        public void Expect(char ch, GraphQLParseContext parseContext, string message)
        {
            if (!TryConsume(ch))
                throw CreateParseException(parseContext, message, position);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool HasOddNumberOfPrecedingBackslashes()
        {
            var count = 0;
            var i = position - 1;
            while (i >= 0 && buffer[i] == '\\')
            {
                count++;
                i--;
            }

            return (count & 1) == 1;
        }
    }

    /// <summary>
    /// Validates that fragment spreads do not form cycles according to GraphQL spec
    /// </summary>
    private static void ValidateFragmentCycles(GraphQLDocument document)
    {
        if (document.Fragments.Count == 0)
            return;

        var fragmentDependencies = new Dictionary<string, HashSet<string>>(document.Fragments.Count);
        foreach (var fragment in document.Fragments.Values)
        {
            var dependencies = new HashSet<string>();
            CollectFragmentDependencies(fragment, dependencies);
            if (dependencies.Count > 0)
            {
                fragmentDependencies[fragment.Name] = dependencies;
            }
        }

        if (fragmentDependencies.Count == 0)
            return;

        var visited = new HashSet<string>(fragmentDependencies.Count);
        var recursionStack = new HashSet<string>();

        foreach (var fragmentName in fragmentDependencies.Keys)
        {
            if (!visited.Contains(fragmentName))
            {
                if (HasFragmentCycle(fragmentName, fragmentDependencies, visited, recursionStack))
                {
                    throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Fragment spreads must not form cycles. Fragment '{fragmentName}' creates a cycle.");
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CollectFragmentDependencies(GraphQLFragmentStatement fragment, HashSet<string> dependencies)
    {
        foreach (var field in fragment.QueryFields)
        {
            CollectFragmentDependenciesFromField(field, dependencies);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CollectFragmentDependenciesFromField(BaseGraphQLField field, HashSet<string> dependencies)
    {
        if (field is GraphQLFragmentSpreadField fragmentSpread)
        {
            dependencies.Add(fragmentSpread.Name);
        }

        foreach (var subField in field.QueryFields)
        {
            CollectFragmentDependenciesFromField(subField, dependencies);
        }
    }

    private static bool HasFragmentCycle(string fragmentName, Dictionary<string, HashSet<string>> dependencies, HashSet<string> visited, HashSet<string> recursionStack)
    {
        visited.Add(fragmentName);
        recursionStack.Add(fragmentName);

        if (dependencies.TryGetValue(fragmentName, out var fragmentDeps))
        {
            foreach (var dependency in fragmentDeps)
            {
                if (recursionStack.Contains(dependency))
                    return true;

                if (!visited.Contains(dependency) && HasFragmentCycle(dependency, dependencies, visited, recursionStack))
                    return true;
            }
        }

        recursionStack.Remove(fragmentName);
        return false;
    }

    private static ExecutableGraphQLStatement CreateOperation(
        GraphQLParseContext parseContext,
        string operationType,
        string? operationName,
        Dictionary<string, ArgType> variables,
        IGraphQLNode node,
        ref SpanReader reader
    )
    {
        ExecutableGraphQLStatement operation;
        switch (operationType)
        {
            case "query":
            {
                var queryParam = Expression.Parameter(node.Schema.QueryContextType, "query_ctx");
                operation = new GraphQLQueryStatement(node.Schema, operationName, queryParam, queryParam, variables);
                break;
            }
            case "mutation":
            {
                var mutationParam = Expression.Parameter(node.Schema.MutationType, "mut_ctx");
                operation = new GraphQLMutationStatement(node.Schema, operationName, mutationParam, mutationParam, variables);
                break;
            }
            case "subscription":
            {
                var subscriptionParam = Expression.Parameter(node.Schema.SubscriptionType, "sub_ctx");
                operation = new GraphQLSubscriptionStatement(node.Schema, operationName, subscriptionParam, variables);
                break;
            }
            default:
                throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Unknown operation type {operationType}");
        }

        parseContext.CurrentOperation = operation;

        var location = operationType switch
        {
            "query" => ExecutableDirectiveLocation.Query,
            "mutation" => ExecutableDirectiveLocation.Mutation,
            "subscription" => ExecutableDirectiveLocation.Subscription,
            _ => throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Unknown operation type {operationType}"),
        };
        var directives = ParseDirectives(parseContext, ref reader, location, node);

        if (directives != null)
        {
            operation.AddDirectives(directives);
        }

        parseContext.CurrentOperation = null;

        return operation;
    }

    private static Dictionary<string, object?> ProcessArguments(GraphQLParseContext parseContext, IField field, Dictionary<string, object?> arguments, IGraphQLNode node)
    {
        if (arguments.Count == 0)
            return arguments;

        foreach (var arg in arguments)
        {
            var argName = arg.Key;
            if (!field.Arguments.ContainsKey(argName))
            {
                throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"No argument '{argName}' found on field '{field.Name}'");
            }

            var argType = field.GetArgumentType(argName);

            if (arg.Value is Expression or VariableReference)
            {
                // Keep Expression and VariableReference as-is, they'll be resolved later during compilation
                arguments[argName] = arg.Value;
            }
            else
            {
                var processedValue = ConvertArgumentValue(node.Schema, arg.Value, argType.Type.TypeDotnet);
                arguments[argName] = processedValue;
            }
        }

        return arguments;
    }

    internal static object? ConvertArgumentValue(ISchemaProvider schema, object? value, Type targetType)
    {
        if (value == null)
            return null;

        if (value is Dictionary<string, object?> dict)
        {
            return ConvertObjectArgument(schema, dict, targetType);
        }

        if (value is List<object?> list)
        {
            return ConvertListArgument(schema, list, targetType);
        }

        if (targetType.IsEnum && value is string enumStr)
        {
            return Enum.Parse(targetType, enumStr, true);
        }

        var underlyingType = Nullable.GetUnderlyingType(targetType);
        if (underlyingType != null && underlyingType.IsEnum && value is string enumStrNullable)
        {
            return Enum.Parse(underlyingType, enumStrNullable, true);
        }

        if (value is Expression or VariableReference)
        {
            // Keep Expression and VariableReference as-is, they'll be resolved later during compilation
            return value;
        }

        return ExpressionUtil.ConvertObjectType(value, targetType, schema);
    }

    private static object ConvertObjectArgument(ISchemaProvider schema, Dictionary<string, object?> dict, Type targetType)
    {
        var constructors = targetType.GetConstructors();
        var constructor = constructors.FirstOrDefault(c => c.GetParameters().Length == 0 || c.GetParameters().Length == dict.Count);

        if (constructor == null)
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"No suitable constructor found for type '{targetType.Name}'");

        var constructorParameters = constructor.GetParameters();
        if (constructorParameters.Length > 0)
        {
            object[] constructorArgs = new object[constructorParameters.Length];

            for (int i = 0; i < constructorParameters.Length; i++)
            {
                var paramName = constructorParameters[i].Name!;
                if (!dict.TryGetValue(paramName, out var paramValue))
                    throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Field '{paramName}' not found in argument object");

                constructorArgs[i] =
                    ConvertArgumentValue(schema, paramValue, constructorParameters[i].ParameterType)
                    ?? throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Field '{paramName}' is null in argument object");
            }

            return constructor.Invoke(constructorArgs);
        }

        var obj = Activator.CreateInstance(targetType)!;
        var schemaType = schema.GetSchemaType(targetType, true, null);
        var propTracker = obj is IArgumentsTracker tracker ? tracker : null;

        foreach (var kvp in dict)
        {
            if (!schemaType.HasField(kvp.Key, null))
                throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Field '{kvp.Key}' not found of type '{schemaType.Name}'");

            var schemaField = schemaType.GetField(kvp.Key, null);
            if (schemaField.ResolveExpression == null)
                throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Field '{kvp.Key}' on type '{schemaType.Name}' has no resolve expression");

            var nameFromType = ((MemberExpression)schemaField.ResolveExpression).Member.Name;
            var prop = targetType.GetProperty(nameFromType);

            if (prop == null)
            {
                var fieldInfo = targetType.GetField(nameFromType);
                if (fieldInfo != null)
                {
                    fieldInfo.SetValue(obj, ConvertArgumentValue(schema, kvp.Value, fieldInfo.FieldType));
                    propTracker?.MarkAsSet(fieldInfo.Name);
                }
            }
            else
            {
                prop.SetValue(obj, ConvertArgumentValue(schema, kvp.Value, prop.PropertyType));
                propTracker?.MarkAsSet(prop.Name);
            }
        }

        return obj;
    }

    private static object ConvertListArgument(ISchemaProvider schema, List<object?> list, Type targetType)
    {
        var elementType = targetType.GetEnumerableOrArrayType() ?? throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, "Could not determine list element type");

        if (targetType.IsArray)
        {
            var array = Array.CreateInstance(elementType, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var convertedValue = ConvertArgumentValue(schema, list[i], elementType);
                array.SetValue(convertedValue, i);
            }
            return array;
        }

        IList result;
        if (targetType.IsInterface && targetType.IsGenericType && targetType.IsGenericTypeEnumerable())
        {
            result = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType), list.Count)!;
        }
        else
        {
            try
            {
                result = (IList)Activator.CreateInstance(targetType, list.Count)!;
            }
            catch
            {
                result = (IList)Activator.CreateInstance(targetType)!;
            }
        }

        for (int i = 0; i < list.Count; i++)
        {
            var convertedValue = ConvertArgumentValue(schema, list[i], elementType);
            result.Add(convertedValue);
        }

        return result;
    }

    /// <summary>
    /// Creates a field. Selection fields are added after creation
    /// </summary>
    private static BaseGraphQLField CreateField(
        GraphQLParseContext parseContext,
        string name,
        string? alias,
        Dictionary<string, object?> arguments,
        List<GraphQLDirective>? directives,
        IGraphQLNode node
    )
    {
        var schemaType = node.Field?.ReturnType.SchemaType ?? node.Schema.GetSchemaType(node.NextFieldContext?.Type ?? node.Schema.QueryContextType, false, null);
        var schemaField = schemaType.GetField(name, null);
        var resultName = alias ?? schemaField.Name;
        var processedArguments = ProcessArguments(parseContext, schemaField, arguments, node);

        BaseGraphQLField field;
        if (schemaField is SubscriptionField subscriptionField)
        {
            var nextContextParam = Expression.Parameter(schemaField.ReturnType.TypeDotnet, $"sub_{schemaField.Name}");
            field = new GraphQLSubscriptionField(node.Schema, resultName, subscriptionField, processedArguments, nextContextParam, nextContextParam, node);
        }
        else if (schemaField is MutationField mutationField)
        {
            var nextContextParam = Expression.Parameter(schemaField.ReturnType.TypeDotnet, $"mut_{schemaField.Name}");
            field = new GraphQLMutationField(node.Schema, resultName, mutationField, processedArguments, nextContextParam, nextContextParam, node);
        }
        else if (schemaField.ReturnType.IsList)
        {
            var elementType = schemaField.ReturnType.SchemaType.TypeDotnet;
            var fieldParam = Expression.Parameter(elementType, $"p_{elementType.Name}");
            field = new GraphQLListSelectionField(
                node.Schema,
                schemaField,
                resultName,
                fieldParam,
                schemaField.FieldParam ?? node.RootParameter,
                schemaField.ResolveExpression!,
                node,
                processedArguments
            );
        }
        else if (schemaField.ReturnType.SchemaType.RequiresSelection)
        {
            var rootParam = schemaField.FieldParam ?? node.RootParameter!;
            field = new GraphQLObjectProjectionField(node.Schema, schemaField, resultName, schemaField.ResolveExpression!, rootParam, node, processedArguments);

            var listExp = ExpressionUtil.FindEnumerable(schemaField.ResolveExpression!);
            if (listExp.Item1 != null && listExp.Item2 != null)
            {
                var returnType = node.Schema.GetSchemaType(listExp.Item1.Type.GetEnumerableOrArrayType()!, node.Field?.FromType.GqlType == GqlTypes.InputObject, null);
                var elementType = returnType.TypeDotnet;
                var listFieldParam = Expression.Parameter(elementType, $"p_{elementType.Name}");
                var listField = new GraphQLListSelectionField(
                    node.Schema,
                    schemaField,
                    resultName,
                    listFieldParam,
                    schemaField.FieldParam ?? node.RootParameter,
                    listExp.Item1,
                    node,
                    processedArguments
                );

                field = new GraphQLCollectionToSingleField(node.Schema, listField, (GraphQLObjectProjectionField)field, listExp.Item2);
            }
        }
        else if (schemaField.ReturnType.SchemaType.IsScalar || schemaField.ReturnType.SchemaType.IsEnum)
        {
            var rootParam = schemaField.FieldParam ?? node.RootParameter;
            field = new GraphQLScalarField(node.Schema, schemaField, resultName, schemaField.ResolveExpression!, rootParam, node, processedArguments);
        }
        else
        {
            throw new EntityGraphQLException(
                GraphQLErrorCategory.DocumentError,
                $"Field '{schemaField.Name}' of type '{schemaField.ReturnType.SchemaType.Name}' can not be queried directly. It is not a scalar and does not require a selection set."
            );
        }

        if (directives != null && directives.Count > 0)
        {
            field.AddDirectives(directives);
        }

        return field;
    }
}

internal sealed class GraphQLVariableType
{
    public string TypeName { get; }
    public bool IsList { get; }
    public bool InnerNonNull { get; }
    public bool OuterNonNull { get; }

    public GraphQLVariableType(string typeName, bool isList, bool innerNonNull, bool outerNonNull)
    {
        TypeName = typeName;
        IsList = isList;
        InnerNonNull = innerNonNull;
        OuterNonNull = outerNonNull;
    }
}

internal ref struct GraphQLParseContext
{
    public ReadOnlySpan<char> Source { get; }
    public QueryVariables QueryVariables { get; }
    public ExecutableGraphQLStatement? CurrentOperation { get; set; }
    public bool InFragment { get; set; }

    public GraphQLParseContext(ReadOnlySpan<char> query, QueryVariables queryVariables)
    {
        Source = query;
        QueryVariables = queryVariables;
    }
}
