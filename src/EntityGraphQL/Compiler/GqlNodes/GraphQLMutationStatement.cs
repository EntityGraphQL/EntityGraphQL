using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Directives;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Represents a GraphQL mutation statement and knows how to execute the mutation fields.
    /// </summary>
    public class GraphQLMutationStatement : ExecutableGraphQLStatement
    {
        public GraphQLMutationStatement(ISchemaProvider schema, string name, Expression nodeExpression, ParameterExpression rootParameter, Dictionary<string, ArgType> variables)
            : base(schema, name, nodeExpression, rootParameter, variables)
        {
        }

        public override async Task<ConcurrentDictionary<string, object?>> ExecuteAsync<TContext>(TContext? context, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, Func<string, string> fieldNamer, ExecutionOptions options, QueryVariables? variables) where TContext : default
        {
            if (context == null && serviceProvider == null)
                throw new EntityGraphQLCompilerException("Either context or serviceProvider must be provided.");

            var result = new ConcurrentDictionary<string, object?>();
            // pass to directvies
            foreach (var directive in Directives)
            {
                if (directive.VisitNode(ExecutableDirectiveLocation.MUTATION, Schema, this, Arguments, null, null) == null)
                    return result;
            }

            // Mutation fields don't directly have services to collect. This is handled after the mutaiton is executed.
            // When we are building/executing the selection on the mutation result services are handled
            CompileContext compileContext = new();
            foreach (var field in QueryFields)
            {
                try
                {
                    object? docVariables = BuildDocumentVariables(ref variables);
                    foreach (var node in field.Expand(compileContext, fragments, false, NextFieldContext!, OpVariableParameter, docVariables).Cast<GraphQLMutationField>())
                    {
#if DEBUG
                        Stopwatch? timer = null;
                        if (options.IncludeDebugInfo)
                        {
                            timer = new Stopwatch();
                            timer.Start();
                        }
#endif

                        var contextToUse = GetContextToUse(context, serviceProvider!, field)!;

                        var data = await ExecuteAsync(compileContext, node, contextToUse, serviceProvider, fragments, options, docVariables);
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

        /// <summary>
        /// Execute the current mutation
        /// </summary>
        /// <param name="node">The mutation field to execute</param>
        /// <param name="context">The context instance that will be used</param>
        /// <param name="validator">Error validator, passed to mutations</param>
        /// <param name="serviceProvider">A service provider to look up any dependencies</param>
        /// <param name="fragments"></param>
        /// <param name="options">Execution options</param>
        /// <param name="docVariables">Resolved values of variables pass in request</param>
        /// <typeparam name="TContext"></typeparam>
        /// <returns></returns>
        private async Task<object?> ExecuteAsync<TContext>(CompileContext compileContext, GraphQLMutationField node, TContext context, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ExecutionOptions options, object? docVariables)
        {
            if (context == null)
                return null;
            // run the mutation to get the context for the query select
            var result = await node.ExecuteMutationAsync(context, serviceProvider, OpVariableParameter, docVariables);

            if (result == null || // result is null and don't need to do anything more
                node.ResultSelection == null) // mutation must return a scalar type
                return result;
            return await MakeSelectionFromResultAsync(compileContext, node, node.ResultSelection!, context, serviceProvider, fragments, options, docVariables, result);
        }

        protected async Task<object?> MakeSelectionFromResultAsync<TContext>(CompileContext compileContext, BaseGraphQLQueryField node, BaseGraphQLQueryField selection, TContext context, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ExecutionOptions options, object? docVariables, object? result)
        {
            var resultExp = selection;

            if (result is LambdaExpression mutationLambda)
            {
                // If they have returned an Expression, we need to join the select Expression with the
                // expression they returned which gives us the full Expression executable over the whole
                // GraphQL schema

                // this will typically be similar to
                // db => db.Entity.Where(filter) or db => db.Entity.First(filter)
                // i.e. they'll be returning a list of items or a specific item
                // We want to take the field selection from the GraphQL query and add a LINQ Select() onto the expression

                var mutationContextParam = mutationLambda.Parameters.First();
                var mutationContextExpression = mutationLambda.Body;

                if (!mutationContextExpression.Type.IsEnumerableOrArray())
                {
                    var listExp = ExpressionUtil.FindEnumerable(mutationContextExpression);
                    if (listExp.Item1 != null && mutationContextExpression.NodeType == ExpressionType.Call)
                    {
                        // yes we can
                        // rebuild the Expression so we keep any ConstantParameters
                        var item1 = listExp.Item1;
                        var collectionNode = new GraphQLListSelectionField(Schema, null, Name, resultExp!.RootParameter, resultExp.RootParameter, item1, node, null);
                        foreach (var queryField in resultExp.QueryFields)
                        {
                            collectionNode.AddField(queryField);
                        }
                        var newNode = new GraphQLCollectionToSingleField(Schema, collectionNode, (GraphQLObjectProjectionField)resultExp, listExp.Item2!);
                        resultExp = newNode;
                    }
                    else
                    {
                        var newNode = new GraphQLObjectProjectionField((GraphQLObjectProjectionField)resultExp, ((LambdaExpression)result).Body);
                        foreach (var queryField in resultExp.QueryFields)
                        {
                            newNode.AddField(queryField);
                        }
                        resultExp = newNode;
                        SetupConstants(mutationContextExpression, compileContext, resultExp.RootParameter!);
                    }
                }
                else
                {
                    // we now know the context as it is dynamically returned in a mutation
                    if (resultExp is GraphQLListSelectionField listField)
                    {
                        listField.ListExpression = mutationContextExpression;
                    }
                    SetupConstants(mutationContextExpression, compileContext, resultExp.RootParameter!);
                }
                resultExp.RootParameter = mutationContextParam;

                (result, _) = await CompileAndExecuteNodeAsync(compileContext, context!, serviceProvider, fragments, resultExp, options, docVariables);
                return result;
            }
            // we now know the context as it is dynamically returned in a mutation
            if (resultExp is GraphQLListSelectionField field)
            {
                var contextParam = Expression.Parameter(result!.GetType());
                resultExp.RootParameter = contextParam;
                field.ListExpression = contextParam;
            }

            // run the query select against the object they have returned directly from the mutation
            (result, _) = await CompileAndExecuteNodeAsync(compileContext, result!, serviceProvider, fragments, resultExp, options, docVariables);
            return result;
        }

        private static void SetupConstants(Expression mutationContextExpression, CompileContext compileContext, ParameterExpression rootParameter)
        {
            // if they just return a constant I.e the entity they just updated. It comes as a member access constant
            if (mutationContextExpression.NodeType == ExpressionType.MemberAccess)
            {
                var me = (MemberExpression)mutationContextExpression;
                if (me.Expression!.NodeType == ExpressionType.Constant)
                {
                    compileContext.AddConstant(null, rootParameter, Expression.Lambda(me).Compile().DynamicInvoke());
                }
            }
            else if (mutationContextExpression.NodeType == ExpressionType.Constant)
            {
                var ce = (ConstantExpression)mutationContextExpression;
                compileContext.AddConstant(null, rootParameter, ce.Value);
            }
        }
    }
}
