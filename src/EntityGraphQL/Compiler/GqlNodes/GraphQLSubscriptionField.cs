using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

public class GraphQLSubscriptionField : BaseGraphQLQueryField
{
    public SubscriptionField SubscriptionField { get; set; }

    public BaseGraphQLQueryField? ResultSelection { get; set; }

    public GraphQLSubscriptionField(
        ISchemaProvider schema,
        string name,
        SubscriptionField subscriptionField,
        Dictionary<string, object?>? args,
        Expression nextFieldContext,
        ParameterExpression rootParameter,
        IGraphQLNode parentNode
    )
        : base(schema, subscriptionField, name, nextFieldContext, rootParameter, parentNode, args)
    {
        this.SubscriptionField = subscriptionField;
    }

    public Task<object?> ExecuteSubscriptionAsync<TContext>(
        TContext context,
        IServiceProvider? serviceProvider,
        ParameterExpression? variableParameter,
        IPropertySetTrackingDto? variablesToUse,
        ExecutionOptions executionOptions
    )
    {
        try
        {
            return SubscriptionField.CallAsync(context, Arguments, serviceProvider, variableParameter, variablesToUse, executionOptions);
        }
        catch (EntityQuerySchemaException e)
        {
            throw new EntityQuerySchemaException($"Error registering subscription: {e.Message}", e);
        }
    }

    protected override Expression? GetFieldExpression(
        CompileContext compileContext,
        IServiceProvider? serviceProvider,
        List<GraphQLFragmentStatement> fragments,
        ParameterExpression? docParam,
        IPropertySetTrackingDto? docVariables,
        ParameterExpression schemaContext,
        bool withoutServiceFields,
        Expression? replacementNextFieldContext,
        List<Type>? possibleNextContextTypes,
        bool contextChanged,
        ParameterReplacer replacer
    )
    {
        if (ResultSelection == null)
            throw new EntityGraphQLCompilerException($"Subscription {Name} should have a result selection");

        return ResultSelection.GetNodeExpression(
            compileContext,
            serviceProvider,
            fragments,
            docParam,
            docVariables,
            schemaContext,
            withoutServiceFields,
            replacementNextFieldContext,
            possibleNextContextTypes,
            contextChanged,
            replacer
        );
    }
}
