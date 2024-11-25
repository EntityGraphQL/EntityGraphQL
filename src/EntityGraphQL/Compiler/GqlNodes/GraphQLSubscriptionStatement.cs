using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
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
    private List<GraphQLFragmentStatement>? fragments;
    private ExecutionOptions? options;
    private object? docVariables;

    public GraphQLSubscriptionStatement(ISchemaProvider schema, string? name, ParameterExpression rootParameter, Dictionary<string, ArgType> variables)
        : base(schema, name, rootParameter, rootParameter, variables) { }

    public override async Task<ConcurrentDictionary<string, object?>> ExecuteAsync<TContext>(
        TContext? context,
        IServiceProvider? serviceProvider,
        List<GraphQLFragmentStatement> fragments,
        Func<string, string> fieldNamer,
        ExecutionOptions options,
        QueryVariables? variables,
        QueryRequestContext requestContext
    )
        where TContext : default
    {
        if (context == null && serviceProvider == null)
            throw new EntityGraphQLCompilerException("Either context or serviceProvider must be provided.");

        Schema.CheckTypeAccess(Schema.GetSchemaType(Schema.SubscriptionType, false, null), requestContext);

        this.fragments = fragments;
        this.options = options;
        this.docVariables = BuildDocumentVariables(ref variables);

        var result = new ConcurrentDictionary<string, object?>();
        // pass to directives
        foreach (var directive in Directives)
        {
            if (directive.VisitNode(ExecutableDirectiveLocation.SUBSCRIPTION, Schema, this, Arguments, null, null) == null)
                return result;
        }

        CompileContext compileContext = new(options, null, requestContext);
        foreach (var field in QueryFields)
        {
            try
            {
                foreach (var node in field.Expand(compileContext, fragments, false, NextFieldContext!, OpVariableParameter, docVariables).Cast<GraphQLSubscriptionField>())
                {
#if DEBUG
                    Stopwatch? timer = null;
                    if (options.IncludeDebugInfo)
                    {
                        timer = new Stopwatch();
                        timer.Start();
                    }
#endif
                    var contextToUse = GetContextToUse(context, serviceProvider!, node)!;
                    var data = await ExecuteAsync(node, contextToUse, serviceProvider, docVariables, options, requestContext);
#if DEBUG
                    if (options.IncludeDebugInfo)
                    {
                        timer?.Stop();
                        result[$"__{node.Name}_timeMs"] = timer?.ElapsedMilliseconds;
                    }
#endif
                    result[node.Name] = data;
                }
            }
            catch (EntityGraphQLValidationException)
            {
                throw;
            }
            catch (EntityGraphQLFieldException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new EntityGraphQLFieldException(field.Name, ex);
            }
        }
        return result;
    }

    private async Task<object?> ExecuteAsync<TContext>(
        GraphQLSubscriptionField node,
        TContext context,
        IServiceProvider? serviceProvider,
        object? docVariables,
        ExecutionOptions executionOptions,
        QueryRequestContext requestContext
    )
    {
        if (context == null)
            return null;

        BaseGraphQLField.CheckFieldAccess(Schema, node.Field, requestContext);

        // execute the subscription set up method. It returns in IObservable<T>
        var result = await node.ExecuteSubscriptionAsync(context, serviceProvider, OpVariableParameter, docVariables, executionOptions);

        if (result == null || node.ResultSelection == null)
            throw new EntityGraphQLExecutionException($"Subscription {node.Name} returned null. It must return an IObservable<T>");

        // result == IObservable<T>
        var returnType =
            result.GetType().GetGenericArgument(typeof(IObservable<>)) ?? throw new EntityGraphQLExecutionException($"Subscription {node.Name} return type does not implement IObservable<T>");
        return new GraphQLSubscribeResult(returnType, result, this, node);
    }

    public object? ExecuteSubscriptionEvent<TQueryContext, TType>(GraphQLSubscriptionField node, TType eventValue, IServiceProvider serviceProvider, QueryRequestContext requestContext)
    {
        return ExecuteSubscriptionEventAsync<TQueryContext, TType>(node, eventValue, serviceProvider, requestContext).GetAwaiter().GetResult();
    }

    public Task<object?> ExecuteSubscriptionEventAsync<TQueryContext, TType>(GraphQLSubscriptionField node, TType eventValue, IServiceProvider serviceProvider, QueryRequestContext requestContext)
    {
        var context = (TQueryContext)serviceProvider.GetRequiredService(typeof(TQueryContext));

        var result = MakeSelectionFromResultAsync(new CompileContext(options!, null, requestContext), node, node.ResultSelection!, context, serviceProvider, fragments!, docVariables, eventValue);
        return result;
    }

    public override void AddField(BaseGraphQLField field)
    {
        if (QueryFields.Count > 0)
            throw new EntityGraphQLCompilerException($"Subscription operations may only have a single root field. Field '{field.Name}' should be used in another operation.");
        field.IsRootField = true;
        QueryFields.Add(field);
    }
}
