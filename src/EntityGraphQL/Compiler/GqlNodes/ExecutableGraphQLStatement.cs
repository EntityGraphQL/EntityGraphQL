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
        public Expression? NextFieldContext { get; }
        public IGraphQLNode? ParentNode { get; }
        public ParameterExpression? RootParameter { get; }
        /// <summary>
        /// Variables that are expected to be passed in to execute this query
        /// </summary>
        protected readonly Dictionary<string, ArgType> opDefinedVariables = new();
        protected readonly ISchemaProvider schema;

        public ParameterExpression? OpVariableParameter { get; }

        public IField? Field { get; }

        public IReadOnlyDictionary<string, object> Arguments { get; }

        public string Name { get; }

        public List<BaseGraphQLField> QueryFields { get; } = new();

        public ExecutableGraphQLStatement(ISchemaProvider schema, string name, Expression nodeExpression, ParameterExpression rootParameter, Dictionary<string, ArgType> opVariables)
        {
            Name = name;
            NextFieldContext = nodeExpression;
            RootParameter = rootParameter;
            opDefinedVariables = opVariables;
            this.schema = schema;
            Arguments = new Dictionary<string, object>();
            if (opDefinedVariables.Any())
            {
                var variableType = LinqRuntimeTypeBuilder.GetDynamicType(opDefinedVariables.ToDictionary(f => f.Key, f => f.Value.RawType));
                OpVariableParameter = Expression.Parameter(variableType, "doc_vars");
            }
        }

        public virtual Task<ConcurrentDictionary<string, object?>> ExecuteAsync<TContext>(TContext context, GraphQLValidator validator, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, Func<string, string> fieldNamer, ExecutionOptions options, QueryVariables? variables)
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
                try
                {
#if DEBUG
                    Stopwatch? timer = null;
                    if (options.IncludeDebugInfo)
                    {
                        timer = new Stopwatch();
                        timer.Start();
                    }
#endif

                    (var data, var didExecute) = CompileAndExecuteNode(new CompileContext(), context, serviceProvider, fragments, fieldNode, options, docVariables);
#if DEBUG
                    if (options.IncludeDebugInfo)
                    {
                        timer?.Stop();
                        result[$"__{fieldNode.Name}_timeMs"] = timer?.ElapsedMilliseconds;
                    }
#endif

                    if (didExecute)
                        result[fieldNode.Name] = data;
                }
                catch (AggregateException aex)
                {
                    var errors = aex.InnerExceptions.SelectMany<Exception, string>(ex => ex is EntityGraphQLValidationException vex ? vex.ValidationErrors : new[] { $"Field '{fieldNode.Name}' - {ex.Message}" });
                    throw new EntityGraphQLValidationException(errors);
                }
                catch (TargetInvocationException ex)
                {
                    throw new EntityGraphQLExecutionException(fieldNode.Name, ex.InnerException!);
                }
                catch (EntityGraphQLValidationException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new EntityGraphQLExecutionException(fieldNode.Name, ex);
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
                    try
                    {
                        var argValue = ExpressionUtil.ChangeType(variables.GetValueOrDefault(name) ?? argType.DefaultValue, argType.RawType, schema);
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

        protected (object? result, bool didExecute) CompileAndExecuteNode(CompileContext compileContext, object context, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, BaseGraphQLField node, ExecutionOptions options, object? docVariables)
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

            if (node.HasAnyServices(fragments) && options.ExecuteServiceFieldsSeparately == true)
            {
                // build this first as NodeExpression may modify ConstantParameters
                // this is without fields that require services
                expression = node.GetNodeExpression(compileContext, serviceProvider, fragments, OpVariableParameter, docVariables, contextParam, withoutServiceFields: true, null, isRoot: true, false, replacer);
                if (expression != null)
                {
                    // execute expression now and get a result that we will then perform a full select over
                    // This part is happening via EntityFramework if you use it
                    (runningContext, _) = ExecuteExpression(expression, runningContext!, contextParam, serviceProvider, replacer, options, compileContext);
                    if (runningContext == null)
                        return (null, true);

                    // the full selection is now on the anonymous type returned by the selection without fields. We don't know the type until now
                    var newContextType = Expression.Parameter(runningContext.GetType(), "_ctx");

                    // new context
                    compileContext = new();

                    // we now know the selection type without services and need to build the full select on that type
                    // need to rebuild the full query
                    expression = node.GetNodeExpression(compileContext, serviceProvider, fragments, OpVariableParameter, docVariables, newContextType, false, replacementNextFieldContext: newContextType, isRoot: true, contextChanged: true, replacer);
                    contextParam = newContextType;
                }
            }

            if (expression == null)
            {
                // just do things normally
                expression = node.GetNodeExpression(compileContext, serviceProvider, fragments, OpVariableParameter, docVariables, contextParam, false, null, isRoot: true, contextChanged: false, replacer);
            }

            var data = ExecuteExpression(expression, runningContext, contextParam, serviceProvider, replacer, options, compileContext);
            return data;
        }

        private (object? result, bool didExecute) ExecuteExpression(Expression? expression, object context, ParameterExpression contextParam, IServiceProvider? serviceProvider, ParameterReplacer replacer, ExecutionOptions options, CompileContext compileContext)
        {
            // they had a query with a directive that was skipped, resulting in an empty query?
            if (expression == null)
                return (null, false);

            var allArgs = new List<object> { context };

            var parameters = new List<ParameterExpression> { contextParam };

            // this is the full requested graph
            // inject dependencies into the fullSelection
            if (serviceProvider != null)
            {
                expression = GraphQLHelper.InjectServices(serviceProvider, compileContext.Services, allArgs, expression, parameters, replacer);
            }

            if (compileContext.ConstantParameters.Any())
            {
                foreach (var item in compileContext.ConstantParameters)
                {
                    expression = replacer.ReplaceByType(expression, item.Key.Type, item.Key);
                }
                parameters.AddRange(compileContext.ConstantParameters.Keys);
                allArgs.AddRange(compileContext.ConstantParameters.Values);
            }

            // evaluate everything
            if (expression.Type.IsEnumerableOrArray() && !expression.Type.IsDictionary())
            {
                expression = ExpressionUtil.MakeCallOnEnumerable("ToList", new[] { expression.Type.GetEnumerableOrArrayType()! }, expression);
            }

            var lambdaExpression = Expression.Lambda(expression, parameters.ToArray());
            // #if DEBUG
            if (options.NoExecution)
                return (null, false);
            // #endif
            return (lambdaExpression.Compile().DynamicInvoke(allArgs.ToArray()), true);
        }

        public void AddField(BaseGraphQLField field)
        {
            QueryFields.Add(field);
        }
    }
}