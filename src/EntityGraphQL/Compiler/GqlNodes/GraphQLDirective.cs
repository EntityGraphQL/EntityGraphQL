using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Directives;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

public class GraphQLDirective
{
    private readonly IDirectiveProcessor processor;
    private readonly Dictionary<string, object> inlineArgValues;
    private readonly string name;

    public GraphQLDirective(string name, IDirectiveProcessor processor, Dictionary<string, object> inlineArgValues)
    {
        this.processor = processor;
        this.inlineArgValues = inlineArgValues;
        this.name = name;
    }

    public Expression? Process(ISchemaProvider schema, Expression fieldExpression, Dictionary<string, object> args, ParameterExpression? docParam, object? docVariables)
    {
        var arguments = ArgumentUtil.BuildArgumentsObject(schema, name, inlineArgValues.MergeNew(args), processor.GetArguments(schema), processor.GetArgumentsType(), docParam, docVariables);

        return processor.ProcessExpression(fieldExpression, arguments);
    }

    public BaseGraphQLField? ProcessField(ISchemaProvider schema, BaseGraphQLField field, Dictionary<string, object> args, ParameterExpression? docParam, object? docVariables)
    {
        var arguments = ArgumentUtil.BuildArgumentsObject(schema, name, inlineArgValues.MergeNew(args), processor.GetArguments(schema), processor.GetArgumentsType(), docParam, docVariables);
        return processor.ProcessField(field, arguments);
    }
}