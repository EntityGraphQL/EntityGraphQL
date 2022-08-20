using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Schema;
using EntityGraphQL.Subscriptions;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Represents a GraphQL subscription statement and knows how to execue 
    /// </summary>
    public class GraphQLSubscriptionStatement : ExecutableGraphQLStatement
    {
        private IServiceProvider? serviceProvider;
        private List<GraphQLFragmentStatement>? fragments;
        private ExecutionOptions? options;
        private object? docVariables;

        public GraphQLSubscriptionStatement(ISchemaProvider schema, string name, ParameterExpression rootParameter, Dictionary<string, ArgType> variables)
            : base(schema, name, rootParameter, rootParameter, variables)
        {
        }

        public override async Task<ConcurrentDictionary<string, object?>> ExecuteAsync<TContext>(TContext context, GraphQLValidator validator, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, Func<string, string> fieldNamer, ExecutionOptions options, QueryVariables? variables)
        {
            this.serviceProvider = serviceProvider;
            this.fragments = fragments;
            this.options = options;
            this.docVariables = BuildDocumentVariables(ref variables);

            var result = new ConcurrentDictionary<string, object?>();
            CompileContext compileContext = new();
            foreach (var field in QueryFields.Cast<GraphQLSubscriptionField>())
            {
                try
                {
                    foreach (var node in field.Expand(compileContext, fragments, true, NextFieldContext!, OpVariableParameter, docVariables).Cast<GraphQLSubscriptionField>())
                    {
#if DEBUG
                        Stopwatch? timer = null;
                        if (options.IncludeDebugInfo)
                        {
                            timer = new Stopwatch();
                            timer.Start();
                        }
#endif
                        var data = await ExecuteAsync(node, context, validator, serviceProvider, docVariables);
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
                catch (EntityGraphQLException ex)
                {
                    throw new EntityGraphQLException(field.Name, ex);
                }
                catch (EntityGraphQLValidationException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new EntityGraphQLExecutionException(field.Name, ex);
                }
            }
            return result;
        }

        private async Task<object?> ExecuteAsync<TContext>(GraphQLSubscriptionField node, TContext context, GraphQLValidator validator, IServiceProvider? serviceProvider, object? docVariables)
        {
            if (context == null)
                return null;
            // execute the subscription set up method. It returns in IObservable<T>
            var result = await node.ExecuteSubscriptionAsync(context, validator, serviceProvider, OpVariableParameter, docVariables);

            if (result == null || node.ResultSelection == null)
                throw new EntityGraphQLExecutionException($"Subscription {node.Name} returned null. It must return an IObservable<T>");

            // result == IObservable<T> 
            return new SubscriptionResult(result.GetType().GetGenericArguments()[0], result, this, node);
        }

        public object? ExecuteSubscriptionEvent<TType>(GraphQLSubscriptionField node, TType eventValue)
        {
            var (result, _) = CompileAndExecuteNode(new CompileContext(), eventValue!, serviceProvider, fragments!, node.ResultSelection!, options!, docVariables);
            return result;
        }
    }
}
