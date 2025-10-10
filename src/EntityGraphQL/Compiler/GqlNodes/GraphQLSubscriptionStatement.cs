using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using EntityGraphQL.Directives;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using EntityGraphQL.Subscriptions;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.Compiler;

/// <summary>
/// Represents a GraphQL subscription statement and knows how to execute it.
/// </summary>
public class GraphQLSubscriptionStatement : GraphQLMutationStatement
{
    private Dictionary<string, GraphQLFragmentStatement>? fragments;
    private ExecutionOptions? options;
    private IArgumentsTracker? docVariables;

    public GraphQLSubscriptionStatement(ISchemaProvider schema, string? name, ParameterExpression rootParameter, Dictionary<string, ArgType> variables)
        : base(schema, name, rootParameter, rootParameter, variables) { }

    protected override ExecutableDirectiveLocation DirectiveLocation => ExecutableDirectiveLocation.Subscription;
    protected override ISchemaType SchemaType => Schema.GetSchemaType(Schema.SubscriptionType, false, null)!;

    public override async Task<(ConcurrentDictionary<string, object?> data, List<GraphQLError> errors)> ExecuteAsync<TContext>(
        TContext? context,
        IServiceProvider? serviceProvider,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        ExecutionOptions options,
        QueryVariables? variables,
        QueryRequestContext requestContext,
        CancellationToken cancellationToken = default
    )
        where TContext : default
    {
        // Store these for later use in subscription event execution
        this.fragments = fragments.ToDictionary(f => f.Key, f => f.Value);
        this.options = options;
        this.docVariables = BuildDocumentVariables(ref variables);

        return await base.ExecuteAsync(context, serviceProvider, fragments, options, variables, requestContext, cancellationToken);
    }

    protected override async Task<(object? data, bool didExecute, List<GraphQLError> errors)> ExecuteOperationField<TContext>(
        CompileContext compileContext,
        BaseGraphQLField field,
        TContext context,
        IServiceProvider? serviceProvider,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        IArgumentsTracker? docVariables
    )
    {
        if (field is not GraphQLSubscriptionField)
            throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, $"Expected a subscription field but got {field.GetType().Name}");

        foreach (var directive in field.Directives)
        {
            if (directive.VisitNode(ExecutableDirectiveLocation.Field, Schema, field, Arguments, null, null) == null)
                return (null, false, []);
        }

        // For subscriptions, we need to expand and execute each subscription field individually
        var data = await ExecuteAsync((GraphQLSubscriptionField)field, context, serviceProvider, docVariables, compileContext);
        return (data, true, new List<GraphQLError>());
    }

    private async Task<object?> ExecuteAsync<TContext>(
        GraphQLSubscriptionField node,
        TContext context,
        IServiceProvider? serviceProvider,
        IArgumentsTracker? docVariables,
        CompileContext compileContext
    )
    {
        if (context == null)
            return null;

        BaseGraphQLField.CheckFieldAccess(Schema, node.Field, compileContext.RequestContext);

        // execute the subscription set up method. It returns in IObservable<T>
        var (result, _) = await node.ExecuteSubscriptionAsync(context, serviceProvider, OpVariableParameter, docVariables, compileContext);

        if (result == null || node.ResultSelection == null)
            throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, $"Subscription {node.Name} returned null. It must return an IObservable<T>");

        // result == IObservable<T>
        var returnType =
            result.GetType().GetGenericArgument(typeof(IObservable<>))
            ?? throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, $"Subscription {node.Name} return type does not implement IObservable<T>");
        return new GraphQLSubscribeResult(returnType, result, this, node);
    }

    public object? ExecuteSubscriptionEvent<TQueryContext, TType>(GraphQLSubscriptionField node, TType eventValue, IServiceProvider serviceProvider, QueryRequestContext requestContext)
    {
        return ExecuteSubscriptionEventAsync<TQueryContext, TType>(node, eventValue, serviceProvider, requestContext).GetAwaiter().GetResult();
    }

    public Task<object?> ExecuteSubscriptionEventAsync<TQueryContext, TType>(GraphQLSubscriptionField node, TType eventValue, IServiceProvider serviceProvider, QueryRequestContext requestContext)
    {
        var context = (TQueryContext)serviceProvider.GetRequiredService(typeof(TQueryContext));

        var result = MakeSelectionFromResultAsync(
            new CompileContext(options!, null, requestContext, OpVariableParameter, docVariables),
            node,
            node.ResultSelection!,
            context,
            serviceProvider,
            fragments!,
            docVariables,
            eventValue
        );
        return result;
    }

    public override void AddField(BaseGraphQLField field)
    {
        if (QueryFields.Count > 0)
            throw new EntityGraphQLException(
                GraphQLErrorCategory.DocumentError,
                $"Subscription operations may only have a single root field. Field '{field.Name}' should be used in another operation."
            );
        field.IsRootField = true;
        QueryFields.Add(field);
    }
}
