using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Base class for document statements that we "execute" - Query and Mutation. Execution runs the expression and gets the data result
    /// A fragment is just a definition
    /// </summary>
    public abstract class ExecutableGraphQLStatement : IGraphQLStatement
    {
        public string Name { get; protected set; }
        public IEnumerable<BaseGraphQLField> QueryFields { get; protected set; }

        public virtual Task<ConcurrentDictionary<string, object>> ExecuteAsync<TContext>(TContext context, GraphQLValidator validator, IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, Func<string, string> fieldNamer)
        {
            // build separate expression for all root level nodes in the op e.g. op is
            // query Op1 {
            //      people { name id }
            //      movies { released name }
            // }
            // people & movies will be the 2 fields that will be 2 separate expressions
            var result = new ConcurrentDictionary<string, object>();
            foreach (var node in QueryFields)
            {
                result[node.Name] = null;
                try
                {
                    var data = CompileAndExecuteNode(context, validator, serviceProvider, fragments, node);

                    result[node.Name] = data;
                }
                catch (Exception)
                {
                    throw new EntityGraphQLCompilerException($"Error executing query field {node.Name}");
                }
            }
            return Task.FromResult(result);
        }

        protected object CompileAndExecuteNode<TContext>(TContext context, GraphQLValidator validator, IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, BaseGraphQLField node)
        {
            var lambdaExpression = Compile(node, context, validator, serviceProvider, fragments, out List<object> allArgs);
            var data = lambdaExpression.Compile().DynamicInvoke(allArgs.ToArray());
            return data;
        }

        protected LambdaExpression Compile<TContext>(BaseGraphQLField node, TContext context, GraphQLValidator validator, IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, out List<object> allArgs)
        {
            allArgs = new List<object> { context };

            // should only be calling Execute at the top level
            var contextParam = node.RootFieldParameter;
            var parameters = new List<ParameterExpression> { contextParam };

            // For root/top level fields we need to first select the whole graph without fields that require services
            // so that EF Core 3.1+ can run and optimise the query
            // We then select the full graph from that context

            var replacer = new ParameterReplacer();
            ExpressionResult expression = null;

            if (node.HasAnyServices)
            {
                // build this first as NodeExpression may modify ConstantParameters
                // this is without fields that require services
                expression = node.GetNodeExpression(context, serviceProvider, fragments, withoutServiceFields: true);
                if (expression != null)
                {
                    // the full selection is now on the anonymous type returned by the selection without fields. We don't know the type until now
                    var newContext = Expression.Parameter(expression.Type);
                    // call ToList() on top level nodes to force evaluation
                    if (expression.Type.IsEnumerableOrArray())
                    {
                        newContext = Expression.Parameter(expression.Type.GetEnumerableOrArrayType());
                        expression = ExpressionUtil.MakeCallOnEnumerable("ToList", new Type[] { newContext.Type }, expression);
                    }
                    // we now know the selection type without services and need to build the full select on that type
                    // need to rebuild it
                    var fullSelection = node.GetNodeExpression(context, serviceProvider, fragments, buildServiceWrapWithParam: newContext);

                    // replace anything that required the normal root level param type with the new type selected above (without service fields)
                    expression = CombineNodeExpressionWithSelection(expression, fullSelection, replacer, newContext);
                }
            }

            if (expression == null) // just do things normally
            {
                // this is already evaluated and will just return the expression
                expression = node.GetNodeExpression(context, serviceProvider, fragments);
            }

            // this is the full requested graph
            // inject dependencies into the fullSelection
            if (serviceProvider != null)
            {
                expression = GraphQLHelper.InjectServices(serviceProvider, node.Services, allArgs, expression, parameters, replacer);
            }

            if (node.ConstantParameters.Any())
            {
                foreach (var item in node.ConstantParameters)
                {
                    expression = (ExpressionResult)replacer.ReplaceByType(expression, item.Key.Type, item.Key);
                }
                parameters.AddRange(node.ConstantParameters.Keys);
                allArgs.AddRange(node.ConstantParameters.Values);
            }

            // evaluate everything
            if (expression.Type.IsEnumerableOrArray())
            {
                expression = ExpressionUtil.MakeCallOnEnumerable("ToList", new Type[] { expression.Type.GetEnumerableOrArrayType() }, expression);
            }

            var lambdaExpression = Expression.Lambda(expression, parameters.ToArray());
            return lambdaExpression;
        }

        /// <summary>
        /// Given a Expression that is the whole graph without service fields, combine it with the full selection graph
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="replacer"></param>
        /// <returns></returns>
        protected ExpressionResult CombineNodeExpressionWithSelection(ExpressionResult expression, ExpressionResult fullNodeExpression, ParameterReplacer replacer, ParameterExpression newContextParam)
        {
            if (fullNodeExpression.NodeType != ExpressionType.Call)
                throw new Exception($"Unexpected NoteType {fullNodeExpression.NodeType}");

            var call = (MethodCallExpression)fullNodeExpression;
            if (call.Method.Name != "Select")
                throw new Exception($"Unexpected method {call.Method.Name}");

            var newExp = replacer.Replace(call.Arguments[1], QueryFields.First().RootFieldParameter, newContextParam);
            if (newExp.NodeType == ExpressionType.Quote)
                newExp = ((UnaryExpression)newExp).Operand;

            if (newExp.NodeType != ExpressionType.Lambda)
                throw new EntityGraphQLCompilerException($"Error compling query. Unexpected expression type {newExp.NodeType}");

            var selection = (LambdaExpression)newExp;
            var newCall = ExpressionUtil.MakeCallOnQueryable("Select", new Type[] { newContextParam.Type, selection.ReturnType }, expression, selection);
            return newCall;
        }
    }
}