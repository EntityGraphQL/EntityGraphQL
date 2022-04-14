using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Base class for document statements that we "execute" - Query and Mutation. Execution runs the expression and gets the data result
    /// A fragment is just a definition
    /// </summary>
    public abstract class ExecutableGraphQLStatement : IGraphQLNode
    {
        public Expression? NextFieldContext { get; set; }
        public IGraphQLNode? ParentNode { get; set; }
        public ParameterExpression? RootParameter { get; set; }
        public string Name { get; protected set; }
        public List<BaseGraphQLField> QueryFields { get; protected set; } = new List<BaseGraphQLField>();
        /// <summary>
        /// Variables that are expected to be passed in to execute this query
        /// </summary>
        protected readonly Dictionary<string, ArgType> opDefinedVariables = new();
        protected readonly ISchemaProvider schema;

        public ParameterExpression? OpVariableParameter { get; }

        public IField? Field { get; }

        public Dictionary<string, object> Arguments { get; }

        public ExecutableGraphQLStatement(ISchemaProvider schema, string name, Expression nodeExpression, ParameterExpression rootParameter, IGraphQLNode parentNode, Dictionary<string, ArgType> opVariables)
        {
            Name = name;
            NextFieldContext = nodeExpression;
            RootParameter = rootParameter;
            ParentNode = parentNode;
            opDefinedVariables = opVariables;
            this.schema = schema;
            Arguments = new Dictionary<string, object>();
            if (opDefinedVariables.Any())
            {
                var variableType = LinqRuntimeTypeBuilder.GetDynamicType(opDefinedVariables.ToDictionary(f => f.Key, f => f.Value.RawType));
                OpVariableParameter = Expression.Parameter(variableType, "doc_vars");
            }
        }

        public virtual Task<ConcurrentDictionary<string, object?>> ExecuteAsync<TContext>(TContext context, GraphQLValidator validator, IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, Func<string, string> fieldNamer, ExecutionOptions options, QueryVariables? variables)
        {
            // build separate expression for all root level nodes in the op e.g. op is
            // query Op1 {
            //      people { name id }
            //      movies { released name }
            // }
            // people & movies will be the 2 fields that will be 2 separate expressions
            var result = new ConcurrentDictionary<string, object?>();
            if (context == null)
                return Task.FromResult(result);

            object? docVariables = BuildDocumentVariables(ref variables);

            foreach (var fieldNode in QueryFields)
            {
                result[fieldNode.Name] = null;
                try
                {
                    Stopwatch? timer = null;
                    if (options.IncludeDebugInfo == true)
                    {
                        timer = new Stopwatch();
                        timer.Start();
                    }

                    var data = CompileAndExecuteNode(context, serviceProvider, fragments, fieldNode, options, docVariables);

                    if (options.IncludeDebugInfo == true)
                    {
                        timer?.Stop();
                        result[$"__{fieldNode.Name}_timeMs"] = timer?.ElapsedMilliseconds;
                    }

                    result[fieldNode.Name] = data;
                }
                catch (Exception ex)
                {
                    throw new EntityGraphQLExecutionException(fieldNode.Name, ex is TargetInvocationException ? ex.InnerException : ex);
                }
            }
            return Task.FromResult(result);
        }

        protected object? BuildDocumentVariables(ref QueryVariables? variables)
        {
            // inject document level variables - letting the query be cached and passing in different variables
            object? variablesToUse = null;

            if (opDefinedVariables.Any() && OpVariableParameter != null)
            {
                if (variables == null)
                    variables = new QueryVariables();
                variablesToUse = Activator.CreateInstance(OpVariableParameter.Type);
                foreach (var (name, argType) in opDefinedVariables)
                {
                    // if (!variables.ContainsKey(name) && value == null)
                    //     throw new EntityGraphQLExecutionException($"Variable {name} is required but was not supplied");
                    try
                    {
                        var argValue = ExpressionUtil.ChangeType(variables.GetValueOrDefault(name) ?? argType.DefaultValue, argType.RawType, null);
                        OpVariableParameter.Type.GetField(name).SetValue(variablesToUse, argValue);
                    }
                    catch (Exception ex)
                    {
                        throw new EntityGraphQLCompilerException($"Supplied variable '{name}' can not be applied to defined variable type '{argType.Type}'", ex);
                    }
                }
            }

            return variablesToUse;
        }

        protected object? CompileAndExecuteNode(object context, IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, BaseGraphQLField node, ExecutionOptions options, object? docVariables)
        {
            object? runningContext = context;

            var replacer = new ParameterReplacer();
            // For root/top level fields we need to first select the whole graph without fields that require services
            // so that EF Core 3.1+ can run and optimise the query against the DB
            // We then select the full graph from that context

            if (node.RootParameter == null)
                throw new EntityGraphQLCompilerException($"Root parameter not set for {node.Name}");

            Expression? expression = null;
            var contextParam = node.RootParameter;

            if (node.HasAnyServices(fragments) && options?.ExecuteServiceFieldsSeparately == true)
            {
                // build this first as NodeExpression may modify ConstantParameters
                // this is without fields that require services
                expression = node.GetNodeExpression(serviceProvider, fragments, OpVariableParameter, docVariables, contextParam, withoutServiceFields: true, isRoot: true);
                if (expression != null)
                {
                    // execute expression now and get a result that we will then perform a full select over
                    // This part is happening via EntityFramework if you use it
                    runningContext = ExecuteExpression(expression, runningContext, contextParam, serviceProvider, node, replacer, options, docVariables);
                    if (runningContext == null)
                        return null;

                    // the full selection is now on the anonymous type returned by the selection without fields. We don't know the type until now
                    var newContextType = Expression.Parameter(runningContext.GetType(), "_ctx");

                    // we now know the selection type without services and need to build the full select on that type
                    // need to rebuild the full query
                    expression = node.GetNodeExpression(serviceProvider, fragments, OpVariableParameter, docVariables, newContextType, false, replacementNextFieldContext: newContextType, isRoot: true, contextChanged: true);
                    contextParam = newContextType;
                }
            }

            if (expression == null)
            {
                // just do things normally
                expression = node.GetNodeExpression(serviceProvider, fragments, OpVariableParameter, docVariables, contextParam, false, isRoot: true);
            }

            var data = ExecuteExpression(expression!, runningContext, contextParam, serviceProvider, node, replacer, options!, docVariables);
            return data;
        }

        protected object? ExecuteExpression(Expression expression, object context, ParameterExpression contextParam, IServiceProvider serviceProvider, BaseGraphQLField node, ParameterReplacer replacer, ExecutionOptions options, object? docVariables)
        {
            var allArgs = new List<object> { context };

            var parameters = new List<ParameterExpression> { contextParam };

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
                    expression = replacer.ReplaceByType(expression, item.Key.Type, item.Key);
                }
                parameters.AddRange(node.ConstantParameters.Keys);
                allArgs.AddRange(node.ConstantParameters.Values);
            }

            // evaluate everything
            if (expression.Type.IsEnumerableOrArray() && !expression.Type.IsDictionary())
            {
                expression = ExpressionUtil.MakeCallOnEnumerable("ToList", new Type[] { expression.Type.GetEnumerableOrArrayType()! }, expression);
            }

            var lambdaExpression = Expression.Lambda(expression, parameters.ToArray());
#if DEBUG
            if (options?.NoExecution == true)
                return null;
#endif
            return lambdaExpression.Compile().DynamicInvoke(allArgs.ToArray());
        }

        public void AddField(BaseGraphQLField field)
        {
            QueryFields.Add(field);
        }
    }
}