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

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Represents a GraphQL subscription statement and knows how to execue 
    /// </summary>
    public class GraphQLSubscriptionStatement : GraphQLMutationStatement
    {
        private IServiceProvider? serviceProvider;
        private List<GraphQLFragmentStatement>? fragments;
        private ExecutionOptions? options;
        private object? docVariables;

        public GraphQLSubscriptionStatement(ISchemaProvider schema, string name, ParameterExpression rootParameter, Dictionary<string, ArgType> variables)
            : base(schema, name, rootParameter, rootParameter, variables)
        {
        }

        public override async Task<ConcurrentDictionary<string, object?>> ExecuteAsync<TContext>(TContext? context, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, Func<string, string> fieldNamer, ExecutionOptions options, QueryVariables? variables) where TContext : default
        {
            this.serviceProvider = serviceProvider;
            this.fragments = fragments;
            this.options = options;
            this.docVariables = BuildDocumentVariables(ref variables);

            var result = new ConcurrentDictionary<string, object?>();
            // pass to directvies
            foreach (var directive in Directives)
            {
                if (directive.VisitNode(ExecutableDirectiveLocation.SUBSCRIPTION, Schema, this, Arguments, null, null) == null)
                    return result;
            }

            CompileContext compileContext = new();
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
                        var data = await ExecuteAsync(node, context, serviceProvider, docVariables);
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

        private async Task<object?> ExecuteAsync<TContext>(GraphQLSubscriptionField node, TContext context, IServiceProvider? serviceProvider, object? docVariables)
        {
            if (context == null)
                return null;
            // execute the subscription set up method. It returns in IObservable<T>
            var result = await node.ExecuteSubscriptionAsync(context, serviceProvider, OpVariableParameter, docVariables);

            if (result == null || node.ResultSelection == null)
                throw new EntityGraphQLExecutionException($"Subscription {node.Name} returned null. It must return an IObservable<T>");

            // result == IObservable<T> 
            var returnType = result.GetType().GetGenericArgument(typeof(IObservable<>));
            if (returnType == null)
                throw new EntityGraphQLExecutionException($"Subscription {node.Name} return type does not implement IObservable<T>");
            return new GraphQLSubscribeResult(returnType, result, this, node);
        }

        public object? ExecuteSubscriptionEvent<TQueryContext, TType>(GraphQLSubscriptionField node, TType eventValue)
        {
            return ExecuteSubscriptionEventAsync<TQueryContext, TType>(node, eventValue).GetAwaiter().GetResult();
        }

        public Task<object?> ExecuteSubscriptionEventAsync<TQueryContext, TType>(GraphQLSubscriptionField node, TType eventValue)
        {
            if (serviceProvider == null)
                throw new EntityGraphQLExecutionException($"serviceProvider cannot be null. Please provide a valid ServiceProvider when executing a subscription operation with the schema query context registered.");

            var context = (TQueryContext)serviceProvider!.GetRequiredService(typeof(TQueryContext));

            var result = MakeSelectionFromResultAsync(new CompileContext(), node, node.ResultSelection!, context, serviceProvider, fragments!, options!, docVariables, eventValue);
            return result;
        }

        public override void AddField(BaseGraphQLField field)
        {
            if (QueryFields.Any())
                throw new EntityGraphQLCompilerException($"Subscription operations may only have a single root field. Field '{field.Name}' should be used in another operation.");
            QueryFields.Add(field);
        }
    }
}
