using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Directives;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.Compiler;

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
    protected internal IReadOnlyDictionary<string, ArgType> OpDefinedVariables { get; set; }
    public ISchemaProvider Schema { get; protected set; }

    public ParameterExpression? OpVariableParameter { get; }

    public IField? Field { get; }
    public bool HasServices => Field?.Services.Count > 0 || Field?.ExecuteAsService == true;

    public IReadOnlyDictionary<string, object?> Arguments { get; }

    public string? Name { get; }

    public List<BaseGraphQLField> QueryFields { get; } = [];
    protected List<GraphQLDirective> Directives { get; } = [];

    /// <summary>
    /// This represents the operation type node which is not a root field
    /// </summary>
    public bool IsRootField => false;

    /// <summary>
    /// The executable directive location for this operation type
    /// </summary>
    protected abstract ExecutableDirectiveLocation DirectiveLocation { get; }

    /// <summary>
    /// The schema type for this operation
    /// </summary>
    protected abstract ISchemaType SchemaType { get; }

    private static int nextStatementId;
    private readonly int statementId = Interlocked.Increment(ref nextStatementId);

    public ExecutableGraphQLStatement(ISchemaProvider schema, string? name, Expression nodeExpression, ParameterExpression rootParameter, IReadOnlyDictionary<string, ArgType> opVariables)
    {
        Name = name;
        NextFieldContext = nodeExpression;
        RootParameter = rootParameter;
        OpDefinedVariables = opVariables;
        Schema = schema;
        Arguments = new Dictionary<string, object?>();
        if (OpDefinedVariables.Count > 0)
        {
            // this type if all the variables defined in the GraphQL document
            // use PropertySetTracking to track is they are set or not (either by a default value or by the user in variables passed in)
            var variableType = LinqRuntimeTypeBuilder.GetDynamicType(OpDefinedVariables.ToDictionary(f => f.Key, f => f.Value.RawType), "docVars", typeof(ArgumentsTracker));
            OpVariableParameter = Expression.Parameter(variableType, "docVars");
        }
    }

    /// <summary>
    /// Abstract method that each operation type must implement to execute their specific field type
    /// </summary>
    protected abstract Task<(object? data, bool didExecute, List<GraphQLError> errors)> ExecuteOperationField<TContext>(
        CompileContext compileContext,
        BaseGraphQLField field,
        TContext context,
        IServiceProvider? serviceProvider,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        IArgumentsTracker? docVariables
    );

    public virtual async Task<(ConcurrentDictionary<string, object?> data, List<GraphQLError> errors)> ExecuteAsync<TContext>(
        TContext? context,
        IServiceProvider? serviceProvider,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        ExecutionOptions options,
        QueryVariables? variables,
        QueryRequestContext requestContext,
        CancellationToken cancellationToken = default
    )
    {
        if (context == null && serviceProvider == null)
            throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, "Either context or serviceProvider must be provided.");

        Schema.CheckTypeAccess(SchemaType, requestContext);

        // build separate expression for all root level nodes in the op e.g. op is
        // query Op1 {
        //      people { name id }
        //      movies { released name }
        // }
        // people & movies will be the 2 fields that will be 2 separate expressions
        var result = new ConcurrentDictionary<string, object?>();
        var allErrors = new List<GraphQLError>();

        // pass to directives
        foreach (var directive in Directives)
        {
            if (directive.VisitNode(DirectiveLocation, Schema, this, Arguments, null, null) == null)
                return (result, allErrors);
        }
        try
        {
            IArgumentsTracker? docVariables = BuildDocumentVariables(ref variables);
            CompileContext compileContext = new(options, null, requestContext, OpVariableParameter, docVariables, cancellationToken);

            if (options.CacheCompiledDelegates && options.EnableQueryCache && options.BeforeExecuting == null)
            {
                // a null hash means a variable value could not be represented by value - skip caching for this
                // request rather than risk serving another request's compiled plan
                var variablesHash = ComputeVariablesHash(variables);
                if (variablesHash != null)
                {
                    compileContext.DelegateCache = Schema.QueryCache;
                    compileContext.DelegateCacheKeyBase = $"{statementId}:{variablesHash}";
                }
            }

            foreach (var fieldNode in QueryFields)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var contextToUse = GetContextToUse(context, serviceProvider!, fieldNode)!;

                    var expandedFields = fieldNode.Expand(compileContext, fragments, false, NextFieldContext!, OpVariableParameter, docVariables).Cast<BaseGraphQLField>();
                    if (!expandedFields.Any())
                        continue;

                    foreach (var expandedField in expandedFields)
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

                            var (data, didExecute, fieldErrors) = await ExecuteOperationField(compileContext, expandedField, contextToUse, serviceProvider, fragments, docVariables);

#if DEBUG
                            if (options.IncludeDebugInfo)
                            {
                                timer?.Stop();
                                result[$"__{expandedField.Name}_timeMs"] = timer?.ElapsedMilliseconds;
                            }
#endif

                            if (fieldErrors.Count > 0)
                            {
                                // if the type is nullable the error bubbles up
                                // should be be on the inner field but the way we resolve full expression trees we don't get the error at that level
                                if (expandedField.Field?.ReturnType.TypeNotNullable == false)
                                    result[expandedField.Name] = null;

                                allErrors.AddRange(fieldErrors);
                            }

                            // often use return null if mutation failed and added errors to validation
                            // don't include it if it is not a nullable field
                            if (data == null && expandedField.Field?.ReturnType.TypeNotNullable == true)
                                continue;

                            if (didExecute)
                                result[expandedField.Name] = data;
                        }
                        catch (EntityGraphQLFieldException fe)
                        {
                            allErrors.Add(new GraphQLError(Schema.AllowedExceptionMessage(fe), expandedField.BuildPath(), null));
                        }
                        catch (EntityGraphQLException ve)
                        {
                            allErrors.AddRange(Schema.GenerateErrors(ve));
                            if (expandedField.Field?.ReturnType.TypeNotNullable == false)
                                result[expandedField.Name] = null;
                        }
                        catch (TargetInvocationException tie) when (tie.InnerException != null)
                        {
                            allErrors.AddRange(Schema.GenerateErrors(tie.InnerException, expandedField.Name));
                            if (expandedField.Field?.ReturnType.TypeNotNullable == false)
                                result[expandedField.Name] = null;
                        }
                        // deferred root lists are enumerated outside the compiled delegate (see
                        // MaterializeDeferredResultAsync) so an AggregateException surfaces here bare rather
                        // than wrapped in a TargetInvocationException - flatten it to distinct errors the same way
                        catch (AggregateException ae)
                        {
                            allErrors.AddRange(Schema.GenerateErrors(ae, expandedField.Name));
                            if (expandedField.Field?.ReturnType.TypeNotNullable == false)
                                result[expandedField.Name] = null;
                        }
                        catch (Exception ex)
                        {
                            allErrors.AddRange(
                                Schema.GenerateErrors(
                                    new EntityGraphQLException(
                                        GraphQLErrorCategory.ExecutionError,
                                        $"Field '{expandedField.Name}' - {Schema.AllowedExceptionMessage(ex)}",
                                        null,
                                        expandedField.BuildPath(),
                                        ex
                                    )
                                )
                            );
                            if (expandedField.Field?.ReturnType.TypeNotNullable == false)
                                result[expandedField.Name] = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, $"Error executing field {fieldNode.Name}", null, fieldNode.BuildPath(), ex);
                }
            }
        }
        // building the variables could cause this
        catch (EntityGraphQLException ce)
        {
            allErrors.AddRange(Schema.GenerateErrors(ce));
        }

        if (allErrors.Count > 0 && result.IsEmpty)
            result = null!; // if we have errors and no data, return null for data

        return (result, allErrors);
    }

    protected static TContext GetContextToUse<TContext>(TContext? context, IServiceProvider serviceProvider, BaseGraphQLField fieldNode)
    {
        if (context == null)
            return serviceProvider.GetService<TContext>()!
                ?? throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, $"Could not find service of type {typeof(TContext).Name} to execute field {fieldNode.Name}");

        return context;
    }

    protected IArgumentsTracker? BuildDocumentVariables(ref QueryVariables? variables)
    {
        // inject document level variables - letting the query be cached and passing in different variables
        IArgumentsTracker? variablesToUse = null;

        if (OpDefinedVariables.Count > 0 && OpVariableParameter != null)
        {
            variables ??= [];
            variablesToUse = (IArgumentsTracker)Activator.CreateInstance(OpVariableParameter.Type)!;
            foreach (var (name, argType) in OpDefinedVariables)
            {
                try
                {
                    object? argValue = null;
                    if (variables.ContainsKey(name) || argType.DefaultValue.IsSet)
                    {
                        argValue = ExpressionUtil.ConvertObjectType(variables.GetValueOrDefault(name) ?? argType.DefaultValue.Value, argType.RawType, Schema);
                        variablesToUse!.MarkAsSet(name);
                    }
                    if (argValue == null && argType.IsRequired)
                        throw new EntityGraphQLException(
                            GraphQLErrorCategory.DocumentError,
                            $"Supplied variable '{name}' is null while the variable definition is non-null. Please update query document or supply a non-null value."
                        );
                    OpVariableParameter.Type.GetField(name)!.SetValue(variablesToUse, argValue);
                }
                catch (EntityGraphQLException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, $"Supplied variable '{name}' can not be applied to defined variable type '{argType.Type}'", null, null, ex);
                }
            }
        }

        return variablesToUse;
    }

    /// <summary>
    /// Builds a hash of the variable values for the delegate cache key. The representation must be value-based -
    /// different values must produce different hashes as they can change the compiled expression shape (e.g. sort
    /// fields, filter inputs). Returns null when a value cannot be represented by value (an arbitrary object with
    /// no ToString override stringifies to its type name), in which case delegate caching is skipped for the
    /// request rather than risking serving another request's compiled plan.
    /// </summary>
    private static string? ComputeVariablesHash(QueryVariables? variables)
    {
        if (variables == null || variables.Count == 0)
            return "";
        var sb = new StringBuilder();
        foreach (var kv in variables.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            sb.Append(kv.Key).Append(':');
            if (!AppendVariableValueForHash(sb, kv.Value))
                return null;
            sb.Append(';');
        }
        return QueryCache.ComputeHash(sb.ToString());
    }

    private static bool AppendVariableValueForHash(StringBuilder sb, object? value)
    {
        switch (value)
        {
            case null:
                sb.Append('\0');
                return true;
            // length-prefixed so adjacent values can't be confused ("ab","c" vs "a","bc")
            case string s:
                sb.Append('s').Append(s.Length).Append(':').Append(s);
                return true;
            case bool b:
                sb.Append(b ? 'T' : 'F');
                return true;
            // numbers, DateTime/DateTimeOffset/TimeSpan, Guid, enums - all format by value
            case IFormattable f:
                sb.Append(f.ToString(null, CultureInfo.InvariantCulture));
                return true;
            case IDictionary dict:
                sb.Append('{');
                foreach (DictionaryEntry entry in dict)
                {
                    if (!AppendVariableValueForHash(sb, entry.Key))
                        return false;
                    sb.Append(':');
                    if (!AppendVariableValueForHash(sb, entry.Value))
                        return false;
                    sb.Append(',');
                }
                sb.Append('}');
                return true;
            case IEnumerable list:
                sb.Append('[');
                foreach (var item in list)
                {
                    if (!AppendVariableValueForHash(sb, item))
                        return false;
                    sb.Append(',');
                }
                sb.Append(']');
                return true;
            default:
                // JsonElement (raw JSON) and anonymous types override ToString with a value-based representation.
                // Anything still using object/ValueType.ToString() would stringify to its type name - identical
                // for different values - so it cannot be safely hashed
                var toStringMethod = value.GetType().GetMethod(nameof(ToString), Type.EmptyTypes);
                if (toStringMethod != null && toStringMethod.DeclaringType != typeof(object) && toStringMethod.DeclaringType != typeof(ValueType))
                {
                    sb.Append(value);
                    return true;
                }
                return false;
        }
    }

    protected async Task<(object? result, bool didExecute)> CompileAndExecuteNodeAsync(
        CompileContext compileContext,
        object context,
        IServiceProvider? serviceProvider,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        BaseGraphQLField node,
        IArgumentsTracker? docVariables
    )
    {
        try
        {
            object? runningContext = context;

            var replacer = new ParameterReplacer();
            // For root/top level fields we need to first select the whole graph without fields that require services
            // so that EF Core can run and optimize the query against the DB
            // We then select the full graph from that context

            if (node.RootParameter == null)
                throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, $"Root parameter not set for {node.Name}");

            // the compileContext is shared across the operation's root fields but bulk resolvers are
            // per-root-field - registered while compiling this node's first pass and resolved against this
            // node's first-pass data. Clear any registered by a previous root field so they are not
            // re-resolved against this node's (differently shaped) data
            compileContext.BulkResolvers.Clear();

            Expression? expression = null;
            var contextParam = node.RootParameter;
            bool isSecondExec = false;

            bool hasServicesAtOrBelow = node.HasServicesAtOrBelow(fragments);
            if (hasServicesAtOrBelow && compileContext.ExecutionOptions.ExecuteServiceFieldsSeparately)
            {
                // build this first as NodeExpression may modify ConstantParameters
                // this is without fields that require services
                expression = node.GetNodeExpression(
                    compileContext,
                    serviceProvider,
                    fragments,
                    OpVariableParameter,
                    docVariables,
                    contextParam,
                    withoutServiceFields: true,
                    null,
                    null,
                    false,
                    replacer
                );
                if (expression != null)
                {
                    // execute expression now and get a result that we will then perform a full select over
                    // This part is happening via EntityFramework if you use it
                    (runningContext, _) = await ExecuteExpressionAsync(expression, runningContext!, contextParam, serviceProvider, replacer, compileContext, node, false, fragments, false);
                    if (runningContext == null)
                        return (null, true);

                    // the full selection is now on the anonymous type returned by the selection without fields. We don't know the type until now
                    var newContextType = Expression.Parameter(runningContext.GetType(), "ctx_no_srv");

                    // core context data is fetched. Now fetch all the bulk resolvers
                    var bulkData = await ResolveBulkLoadersAsync(compileContext, serviceProvider, node, runningContext, replacer, newContextType);

                    // new context
                    var prevDelegateCache = compileContext.DelegateCache;
                    var prevDelegateCacheKeyBase = compileContext.DelegateCacheKeyBase;
                    var newCompileContext = new CompileContext(compileContext.ExecutionOptions, bulkData, compileContext.RequestContext, OpVariableParameter, docVariables, compileContext.CancellationToken)
                    {
                        DelegateCache = prevDelegateCache,
                        DelegateCacheKeyBase = prevDelegateCacheKeyBase,
                    };
                    // the second pass needs the dynamic types the first pass produced for interface/union selections
                    newCompileContext.CopyPossibleNextContextTypesFrom(compileContext);
                    compileContext = newCompileContext;

                    // we now know the selection type without services and need to build the full select on that type
                    // need to rebuild the full query
                    expression = node.GetNodeExpression(
                        compileContext,
                        serviceProvider,
                        fragments,
                        OpVariableParameter,
                        docVariables,
                        newContextType,
                        false,
                        replacementNextFieldContext: newContextType,
                        null,
                        contextChanged: true,
                        replacer
                    );
                    contextParam = newContextType;
                    isSecondExec = true;
                }
            }

            // no services, or not doing it in 2 steps, build full expression now
            expression ??= node.GetNodeExpression(compileContext, serviceProvider, fragments, OpVariableParameter, docVariables, contextParam, false, null, null, contextChanged: false, replacer);

            var data = await ExecuteExpressionAsync(expression, runningContext, contextParam, serviceProvider, replacer, compileContext, node, true, fragments, isSecondExec);
            return data;
        }
        catch (EntityGraphQLFieldException fe)
        {
            throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, $"Field '{node.Name}' - {fe.Message}");
        }
    }

    private static async Task<Dictionary<string, object>> ResolveBulkLoadersAsync(
        CompileContext compileContext,
        IServiceProvider? serviceProvider,
        BaseGraphQLField node,
        object runningContext,
        ParameterReplacer replacer,
        ParameterExpression newContextParam
    )
    {
        var bulkData = new Dictionary<string, object>();
        if (compileContext.BulkResolvers?.Count > 0)
        {
            var bulkTasks = new List<Task>();
            var bulkResults = new Dictionary<string, Task<object>>();

            foreach (var bulkResolver in compileContext.BulkResolvers)
            {
                compileContext.CancellationToken.ThrowIfCancellationRequested();

                // rebuild list expression on new context
                var toReplace = node.Field!.ResolveExpression!;
                var listExpression = bulkResolver.GetBulkSelectionExpression(newContextParam, bulkResolver.ListExpressionPath.GetRange(1, bulkResolver.ListExpressionPath.Count - 1), replacer);
                // var listExpression = replacer.Replace(bulkResolver.GetListExpression(runningContext, newContextParam, replacer), toReplace, newContextParam);
                var newParam = Expression.Parameter(listExpression.Type.GetEnumerableOrArrayType()!, "bulkList");
                // replace the data selection expression with the new context
                var expReplacer = new ExpressionReplacer(bulkResolver.ExtractedFields, newParam, false, false, null);
                var selection = expReplacer.Replace(bulkResolver.DataSelection.Body);
                var selectionLambda = Expression.Lambda(selection, newParam);
                listExpression = Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.Where),
                    [newParam.Type],
                    listExpression,
                    Expression.Lambda(Expression.NotEqual(newParam, Expression.Constant(null)), newParam)
                );
                listExpression = Expression.Call(typeof(Enumerable), nameof(Enumerable.Select), [newParam.Type, selection.Type], listExpression, selectionLambda);
                // listExpression = Expression.Call(typeof(Enumerable), nameof(Enumerable.ToList), [listExpression.Type.GetEnumerableOrArrayType()!], listExpression);

                // the selected IDs to load the bulk data
                var bulkDataArgs = Expression.Lambda(listExpression, newContextParam).Compile().DynamicInvoke([runningContext]);
                var parameters = new List<ParameterExpression> { bulkResolver.FieldExpression.Parameters.First() };
                var allArgs = new List<object?> { bulkDataArgs };
                var bulkLoader = GraphQLHelper.InjectServices(
                    serviceProvider,
                    compileContext.Services,
                    allArgs,
                    bulkResolver.FieldExpression.Body,
                    parameters,
                    replacer,
                    compileContext.RequestContext,
                    compileContext.CancellationToken
                );
                if (compileContext.ConstantParameters.Any())
                {
                    parameters.AddRange(compileContext.ConstantParameters.Keys);
                    allArgs.AddRange(compileContext.ConstantParameters.Values);
                }

                var lambdaExpression = Expression.Lambda(bulkLoader, parameters);

                // Handle async bulk resolvers with concurrency control
                if (bulkResolver.IsAsync)
                {
                    var bulkTask = ExecuteBulkResolverWithConcurrencyAsync(lambdaExpression, [.. allArgs], bulkResolver, compileContext);
                    bulkResults[bulkResolver.Name] = bulkTask!;
                    bulkTasks.Add(bulkTask);
                }
                else
                {
                    var dataLoaded = lambdaExpression.Compile().DynamicInvoke([.. allArgs])!;
                    bulkData[bulkResolver.Name] = dataLoaded;
                }
            }

            // Wait for all async bulk resolvers to complete
            if (bulkTasks.Count > 0)
            {
                await Task.WhenAll(bulkTasks);

                // Collect results from async operations
                foreach (var kvp in bulkResults)
                {
                    bulkData[kvp.Key] = await kvp.Value;
                }
            }
        }

        return bulkData;
    }

    private static async Task<object> ExecuteBulkResolverWithConcurrencyAsync(LambdaExpression lambdaExpression, object?[] args, CompiledBulkFieldResolver bulkResolver, CompileContext compileContext)
    {
        // Generate semaphore configurations for bulk resolver concurrency limiting
        var semaphoreConfigs = GetBulkResolverSemaphoreConfigs(bulkResolver, compileContext);

        if (semaphoreConfigs.Count > 0)
        {
            // Use the existing ExecuteWithConcurrencyLimitAsync method
            return await ConcurrencyLimitFieldExtension.ExecuteWithConcurrencyLimitAsync<object>(
                    lambdaExpression,
                    semaphoreConfigs,
                    compileContext.ConcurrencyLimiterRegistry,
                    args.Where(a => a != null).ToArray()!,
                    compileContext.CancellationToken
                ) ?? new object();
        }

        // No concurrency limiting, execute directly
        var result = lambdaExpression.Compile().DynamicInvoke(args);

        if (result is Task task)
        {
            await task;

            // Get result from Task<T>
            var taskType = task.GetType();
            var resultProperty = taskType.GetProperty(nameof(Task<object>.Result));
            if (resultProperty != null)
            {
                return resultProperty.GetValue(task)!;
            }
        }

        return result!;
    }

    private static List<(string scopeKey, int maxConcurrency)> GetBulkResolverSemaphoreConfigs(CompiledBulkFieldResolver bulkResolver, CompileContext compileContext)
    {
        var configs = new List<(string scopeKey, int maxConcurrency)>();

        // Query-level limit
        if (compileContext.ExecutionOptions.MaxQueryConcurrency.HasValue)
        {
            configs.Add(("query_global", compileContext.ExecutionOptions.MaxQueryConcurrency.Value));
        }

        var serviceMax = compileContext.ExecutionOptions.ServiceConcurrencyLimits.GetValueOrDefault(bulkResolver.ServiceType);
        if (serviceMax > 0)
            configs.Add(($"service_{bulkResolver.ServiceType.AssemblyQualifiedName}", serviceMax));

        // Bulk resolver-specific limit
        if (bulkResolver.MaxConcurrency.HasValue)
        {
            configs.Add(($"bulk_{bulkResolver.Name}", bulkResolver.MaxConcurrency.Value));
        }

        return configs;
    }

    private static async Task<(object? result, bool didExecute)> ExecuteExpressionAsync(
        Expression? expression,
        object context,
        ParameterExpression contextParam,
        IServiceProvider? serviceProvider,
        ParameterReplacer replacer,
        CompileContext compileContext,
        BaseGraphQLField node,
        bool isFinal,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        bool isSecondExec
    )
    {
        // they had a query with a directive that was skipped, resulting in an empty query?
        if (expression == null)
            return (null, false);

        var allArgs = new List<object?> { context };

        var parameters = new List<ParameterExpression> { contextParam };

        // this is the full requested graph
        // inject dependencies into the fullSelection. Runs even without a service provider as the engine
        // supplies some values itself (CancellationToken, QueryRequestContext)
        expression = GraphQLHelper.InjectServices(serviceProvider, compileContext.Services, allArgs, expression, parameters, replacer, compileContext.RequestContext, compileContext.CancellationToken);

        if (compileContext.ConstantParameters.Any())
        {
            parameters.AddRange(compileContext.ConstantParameters.Keys);
            allArgs.AddRange(compileContext.ConstantParameters.Values);
        }

        if (compileContext.BulkData != null)
        {
            parameters.Add(compileContext.BulkParameter!);
            allArgs.Add(compileContext.BulkData);
        }

        if (compileContext.ExecutionOptions.BeforeExecuting != null)
        {
            expression = compileContext.ExecutionOptions.BeforeExecuting.Invoke(expression, isFinal);
        }

        var parametersArray = parameters.ToArray();
        var argsArray = allArgs.ToArray();
        var lambdaExpression = Expression.Lambda(expression, parametersArray);

        Delegate compiledDelegate;
        var delegateCache = compileContext.DelegateCache;
        string? delegateKey = delegateCache != null && compileContext.DelegateCacheKeyBase != null ? $"{compileContext.DelegateCacheKeyBase}:{(isSecondExec ? 2 : 1)}" : null;

        if (delegateKey != null && delegateCache!.GetDelegate(delegateKey) is Delegate cached)
        {
            compiledDelegate = cached;
        }
        else
        {
            // When caching, emit IL so repeated invocations are fast.
            // When not caching (compile-once-use-once per request), prefer interpretation —
            // it skips IL emission and is significantly faster, especially on .NET 10.
            compiledDelegate = lambdaExpression.Compile(preferInterpretation: delegateKey == null);
            if (delegateKey != null)
                delegateCache!.AddDelegate(delegateKey, compiledDelegate);
        }

#if DEBUG
        // do after hitting cache so we can test i.e. no execution oly disables execution not cache lookup etc.
        if (compileContext.ExecutionOptions.NoExecution)
            return (null, false);
#endif

        object? res = null;
        if (lambdaExpression.ReturnType.IsGenericType && lambdaExpression.ReturnType.IsAsyncGenericType())
            res = await (dynamic?)compiledDelegate.DynamicInvoke(argsArray);
        else
            res = compiledDelegate.DynamicInvoke(argsArray);

        // Root list fields on the database-bound pass are built without an in-tree ToList() so the deferred
        // query reaches here - materialize it before further processing. string/IList/IDictionary are
        // already-materialized shapes and pass through untouched
        if (res is IEnumerable and not string and not IList and not IDictionary)
            res = await MaterializeDeferredResultAsync(res, compileContext.CancellationToken);

        // Resolve any nested async results in the returned object graph if the query contains async fields
        if (res != null && node.HasAsyncFieldsAtOrBelow(fragments) && (!compileContext.ExecutionOptions.ExecuteServiceFieldsSeparately || isSecondExec))
            res = await ResolveAsyncResultsRecursive(res, compileContext.CancellationToken);

        return (res, true);
    }

    public virtual void AddField(BaseGraphQLField field)
    {
        // root fields with the same response name must be mergeable per the GraphQL spec. Fragment spreads
        // are named after the fragment, not a response name - they merge during expansion
        if (field is not GraphQLFragmentSpreadField and not GraphQLInlineFragmentField)
        {
            var existing = QueryFields.FirstOrDefault(f => f.Name == field.Name && f is not GraphQLFragmentSpreadField and not GraphQLInlineFragmentField);
            if (existing != null)
                BaseGraphQLField.ValidateFieldsCanMerge(existing, field);
        }
        field.IsRootField = true;
        QueryFields.Add(field);
    }

    public void AddDirectives(IEnumerable<GraphQLDirective> graphQLDirectives)
    {
        Directives.AddRange(graphQLDirectives);
    }

    /// <summary>
    /// Recursively walks the object graph and awaits any async values (Task, ValueTask, IAsyncEnumerable)
    /// </summary>
    private static async Task<object?> ResolveAsyncResultsRecursive(object obj, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var type = obj.GetType();

        // Handle Task<T> and internal async state machines - await it and recursively resolve the result
        if (obj is Task task)
        {
            await task;

            var resultAccessor = taskResultAccessors.GetOrAdd(task.GetType(), TaskResultAccessorFactory);
            if (resultAccessor != null)
            {
                var taskResult = resultAccessor(task);
                return taskResult != null ? await ResolveAsyncResultsRecursive(taskResult, cancellationToken) : null;
            }

            return null; // Task (not Task<T>)
        }

        // non-generic ValueTask - just await it, there is no result
        if (obj is ValueTask plainValueTask)
        {
            await plainValueTask;
            return null;
        }

        // ValueTask<T>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var asTaskMethod = type.GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance);
            if (asTaskMethod != null)
            {
                var valueTaskAsTask = (Task?)asTaskMethod.Invoke(obj, null);
                if (valueTaskAsTask != null)
                {
                    await valueTaskAsTask;
                    var resultProperty = valueTaskAsTask.GetType().GetProperty(nameof(Task<object>.Result));
                    var taskResult = resultProperty?.GetValue(valueTaskAsTask);
                    return taskResult != null ? await ResolveAsyncResultsRecursive(taskResult, cancellationToken) : null;
                }
            }
            return null;
        }

        // IAsyncEnumerable<T> - buffer to a list
        if (ImplementsIAsyncEnumerable(type))
        {
            return await BufferAsyncEnumerable(obj, cancellationToken);
        }

        // If the type's shape can not (transitively) contain an async value there is nothing to resolve.
        // This avoids reflecting over (and rebuilding) plain entity graphs and typed collections - reading
        // every property of e.g. an EF entity can also trigger lazy-loading of navigations the query never
        // selected
        if (!TypeCouldContainAsyncValue(type))
            return obj;

        // Dictionaries must stay dictionaries (the generic collection handling below would turn them into a
        // list of KeyValuePairs, changing the serialized shape). Keys can not be async - resolve the values
        if (obj is IDictionary dictionaryObj)
        {
            var keyType = type.IsGenericType && type.GetGenericArguments().Length == 2 ? type.GetGenericArguments()[0] : typeof(object);
            var resolvedDict = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, typeof(object)))!;
            foreach (DictionaryEntry entry in dictionaryObj)
            {
                resolvedDict[entry.Key] = entry.Value != null ? await ResolveAsyncResultsRecursive(entry.Value, cancellationToken) : null;
            }
            return resolvedDict;
        }

        // Handle collections (but not strings)
        if (obj is IEnumerable enumerable and not string)
        {
            var items = enumerable.Cast<object?>().ToArray();
            var resolvedItems = new List<object?>(items.Length);

            // Process items concurrently
            var tasks = items.Select(async item =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return item != null ? await ResolveAsyncResultsRecursive(item, cancellationToken) : null;
            });

            var results = await Task.WhenAll(tasks);
            resolvedItems.AddRange(results);

            // Try to preserve the original collection type
            var originalType = obj.GetType();

            // Handle arrays
            if (originalType.IsArray)
            {
                var elementType = originalType.GetElementType()!;
                var array = Array.CreateInstance(elementType, resolvedItems.Count);
                for (int i = 0; i < resolvedItems.Count; i++)
                {
                    array.SetValue(resolvedItems[i], i);
                }
                return array;
            }

            // Try to return a List<T> matching the IEnumerable<T> element type so that
            // the resolved collection is assignable back to typed fields (e.g. IEnumerable<T>
            // produced by an async service field).
            var enumerableInterface =
                originalType.IsGenericType && originalType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                    ? originalType
                    : originalType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            if (enumerableInterface != null)
            {
                var elementType = enumerableInterface.GetGenericArguments()[0];
                if (CanMaterializeTypedCollection(elementType, resolvedItems))
                {
                    var typedList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
                    foreach (var item in resolvedItems)
                        typedList.Add(item);
                    return typedList;
                }
            }

            return resolvedItems;
        }

        // Handle complex objects (including anonymous types and dynamic types)
        if (type.IsClass && type != typeof(string) && !type.IsPrimitive)
        {
            return await ResolveComplexObject(obj, type, cancellationToken);
        }

        return obj;
    }

    private static bool CanMaterializeTypedCollection(Type elementType, IEnumerable<object?> items)
    {
        return items.All(item => IsCompatibleCollectionItem(elementType, item));
    }

    private static bool IsCompatibleCollectionItem(Type elementType, object? item)
    {
        if (item == null)
            return !elementType.IsValueType || Nullable.GetUnderlyingType(elementType) != null;

        return elementType.IsInstanceOfType(item);
    }

    private static readonly ConcurrentDictionary<Type, bool> typeCouldContainAsyncCache = new();

    /// <summary>
    /// True if a value of the declared type could be - or could transitively hold - an async value
    /// (Task/ValueTask/IAsyncEnumerable). Based on the declared type graph so a runtime subclass adding an
    /// async member to a non-async declared type is not seen - engine-produced async values always live in
    /// dynamic projection types which are detected. Results are cached per type.
    /// </summary>
    private static bool TypeCouldContainAsyncValue(Type type)
    {
        if (typeCouldContainAsyncCache.TryGetValue(type, out var cached))
            return cached;
        var result = TypeCouldContainAsyncValue(type, new HashSet<Type>());
        typeCouldContainAsyncCache[type] = result;
        return result;
    }

    private static bool TypeCouldContainAsyncValue(Type type, HashSet<Type> visiting)
    {
        if (typeCouldContainAsyncCache.TryGetValue(type, out var cached))
            return cached;
        // a cycle back to a type already being computed can not introduce an async value not found elsewhere
        if (!visiting.Add(type))
            return false;

        try
        {
            if (typeof(Task).IsAssignableFrom(type) || type == typeof(ValueTask) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>)) || ImplementsIAsyncEnumerable(type))
                return true;

            // unknown at compile time - assume it could
            if (type == typeof(object))
                return true;

            // dynamic projection types (where the engine puts Task-typed fields) and anonymous types fall
            // through to the member-type recursion below - only branches whose declared field types can hold
            // an async value are walked, so non-async branches of a result are returned untouched

            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying.IsPrimitive || underlying.IsEnum || underlying == typeof(string) || underlying == typeof(decimal) || underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(TimeSpan) || underlying == typeof(Guid))
                return false;

            if (type.IsArray)
                return TypeCouldContainAsyncValue(type.GetElementType()!, visiting);

            var enumerableInterface =
                type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>) ? type : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            if (enumerableInterface != null)
                // covers dictionaries too - KeyValuePair<K,V> is checked as the element type
                return TypeCouldContainAsyncValue(enumerableInterface.GetGenericArguments()[0], visiting);
            // non-generic IEnumerable - element type unknown, assume it could
            if (typeof(IEnumerable).IsAssignableFrom(type))
                return true;

            // any other class/struct - check the declared types of its public members
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.CanRead && TypeCouldContainAsyncValue(prop.PropertyType, visiting))
                    return true;
            }
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (TypeCouldContainAsyncValue(field.FieldType, visiting))
                    return true;
            }
            return false;
        }
        finally
        {
            visiting.Remove(type);
        }
    }

    private static readonly ConcurrentDictionary<Type, (PropertyInfo[] Properties, FieldInfo[] Fields)> reflectionMemberCache = new();

    private static (PropertyInfo[] Properties, FieldInfo[] Fields) GetCachedMembers(Type type) =>
        reflectionMemberCache.GetOrAdd(type, t => (t.GetProperties(BindingFlags.Public | BindingFlags.Instance), t.GetFields(BindingFlags.Public | BindingFlags.Instance)));

    /// <summary>
    /// Handles complex objects including anonymous types, dynamic types, and regular classes
    /// </summary>
    private static async Task<object?> ResolveComplexObject(object obj, Type type, CancellationToken cancellationToken = default)
    {
        var (properties, fields) = GetCachedMembers(type);

        // For anonymous types and dynamically generated types, try to reconstruct the object
        if (IsAnonymousOrDynamicType(type))
        {
            // all types should be anonymous types built by LinqRuntimeTypeBuilder
            return await RebuildDynamicTypeWithResolvedFields(obj, type, properties, fields, cancellationToken);
        }

        // For regular mutable objects, we can modify in place. Check the declared member type before touching
        // the getter - reading a property can have side effects (e.g. EF lazy-loading)
        foreach (var prop in properties.Where(p => p.CanRead && p.CanWrite))
        {
            if (!TypeCouldContainAsyncValue(prop.PropertyType))
                continue;
            var value = prop.GetValue(obj);
            if (value != null && ContainsAsyncValue(value))
            {
                var resolvedValue = await ResolveAsyncResultsRecursive(value, cancellationToken);
                prop.SetValue(obj, resolvedValue);
            }
        }

        // Fields are typically readonly, but let's try anyway
        foreach (var field in fields.Where(f => !f.IsInitOnly))
        {
            if (!TypeCouldContainAsyncValue(field.FieldType))
                continue;
            var value = field.GetValue(obj);
            if (value != null && ContainsAsyncValue(value))
            {
                var resolvedValue = await ResolveAsyncResultsRecursive(value, cancellationToken);
                field.SetValue(obj, resolvedValue);
            }
        }

        return obj;
    }

    /// <summary>
    /// Checks if a value is or contains async operations
    /// </summary>
    private static bool ContainsAsyncValue(object? value)
    {
        if (value == null)
            return false;
        var t = value.GetType();
        if (typeof(Task).IsAssignableFrom(t))
            return true;
        if (t == typeof(ValueTask) || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ValueTask<>)))
            return true;
        if (ImplementsIAsyncEnumerable(t))
            return true;
        return false;
    }

    /// <summary>
    /// Determines if a type is anonymous or dynamically generated (like those created by EntityGraphQL).
    /// These are rebuilt (not modified in place) as their members are read-only or Task-typed.
    /// </summary>
    private static bool IsAnonymousOrDynamicType(Type type)
    {
        // runtime-built projection types (LinqRuntimeTypeBuilder)
        if (type.Assembly.IsDynamic)
            return true;
        // compiler-generated anonymous types - read-only properties so they must be rebuilt
        if (type.Namespace == null && type.Name.Contains("AnonymousType") && type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
            return true;
        // the engine's own wrapper types (e.g. Connection<T>). Restricted to this assembly - previously any
        // type in an EntityGraphQL.* namespace matched, wrongly rebuilding user types in such namespaces
        return type.Namespace?.StartsWith("EntityGraphQL", StringComparison.Ordinal) == true && type.Assembly == typeof(ExecutableGraphQLStatement).Assembly;
    }

    /// <summary>
    /// A compiled per-type plan for rebuilding a dynamic/anonymous type with its async members resolved -
    /// avoids per-row reflection (member Get/SetValue, Activator, dynamic type lookups). Built by observing
    /// the first row of a type through the reflective path, then reused for every following row. A row whose
    /// resolved member types do not match the plan (e.g. polymorphic nested results) falls back to the
    /// reflective path for that row.
    /// </summary>
    private sealed class DynamicTypeResolutionPlan(string[] memberNames, Func<object, object?>[] getters, bool[] needsResolve, Type[] targetMemberTypes, Func<object?[], object> construct)
    {
        public string[] MemberNames { get; } = memberNames;
        public Func<object, object?>[] Getters { get; } = getters;
        public bool[] NeedsResolve { get; } = needsResolve;
        public Type[] TargetMemberTypes { get; } = targetMemberTypes;
        public Func<object?[], object> Construct { get; } = construct;
    }

    private static readonly ConcurrentDictionary<Type, DynamicTypeResolutionPlan> resolutionPlans = new();

    /// <summary>
    /// Compiled Task&lt;T&gt;.Result accessors keyed by task type - the walker unwraps many tasks per request
    /// and reflection GetProperty/GetValue per task is measurable. Null for non-generic Task.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Func<Task, object?>?> taskResultAccessors = new();

    private static readonly Func<Type, Func<Task, object?>?> TaskResultAccessorFactory = static t =>
    {
        var resultProp = t.GetProperty(nameof(Task<object>.Result));
        if (resultProp == null)
            return null;
        var taskParam = Expression.Parameter(typeof(Task), "task");
        return Expression.Lambda<Func<Task, object?>>(Expression.Convert(Expression.Property(Expression.Convert(taskParam, t), resultProp), typeof(object)), taskParam).Compile();
    };

    /// <summary>
    /// Rebuilds a dynamic type with resolved field types (converting Task<T> fields to T fields)
    /// </summary>
    private static async Task<object?> RebuildDynamicTypeWithResolvedFields(object obj, Type originalType, PropertyInfo[] properties, FieldInfo[] fields, CancellationToken cancellationToken = default)
    {
        // fast path - a previous row of this type compiled a plan
        if (resolutionPlans.TryGetValue(originalType, out var plan))
        {
            var values = new object?[plan.Getters.Length];
            var planMatches = true;
            for (var i = 0; i < plan.Getters.Length; i++)
            {
                var value = plan.Getters[i](obj);
                object? resolved;
                if (value == null || !plan.NeedsResolve[i])
                {
                    resolved = value;
                }
                // fast path - an already-completed task unwraps synchronously without the async recursion
                else if (value is Task { IsCompletedSuccessfully: true } completedTask && taskResultAccessors.GetOrAdd(completedTask.GetType(), TaskResultAccessorFactory) is { } accessor)
                {
                    var taskResult = accessor(completedTask);
                    resolved = taskResult != null && TypeCouldContainAsyncValue(taskResult.GetType()) ? await ResolveAsyncResultsRecursive(taskResult, cancellationToken) : taskResult;
                }
                else
                {
                    resolved = await ResolveAsyncResultsRecursive(value, cancellationToken);
                }
                if (resolved != null && !plan.TargetMemberTypes[i].IsInstanceOfType(resolved))
                {
                    // this row's resolved shape differs from the observed plan - use the reflective path
                    planMatches = false;
                    break;
                }
                values[i] = resolved;
            }
            if (planMatches)
                return plan.Construct(values);
        }

        var fieldTypeMap = new Dictionary<string, Type>();
        var fieldValues = new Dictionary<string, object?>();
        // recorded in iteration order so a plan can be compiled from what we observe
        var memberOrder = new List<(string Name, Type DeclaredType, MemberInfo Member)>();

        // Process properties. Members whose declared type can not contain an async value are copied as-is
        foreach (var prop in properties.Where(p => p.CanRead))
        {
            var value = prop.GetValue(obj);
            var resolvedValue = value != null && TypeCouldContainAsyncValue(prop.PropertyType) ? await ResolveAsyncResultsRecursive(value, cancellationToken) : value;

            fieldValues[prop.Name] = resolvedValue;
            // If original type was Task<T>, use T. Otherwise use the resolved value type or original type
            fieldTypeMap[prop.Name] = GetResolvedFieldType(prop.PropertyType, resolvedValue);
            memberOrder.Add((prop.Name, prop.PropertyType, prop));
        }

        // Process fields
        foreach (var field in fields)
        {
            var value = field.GetValue(obj);
            var resolvedValue = value != null && TypeCouldContainAsyncValue(field.FieldType) ? await ResolveAsyncResultsRecursive(value, cancellationToken) : value;

            fieldValues[field.Name] = resolvedValue;
            // If original type was Task<T>, use T. Otherwise use the resolved value type or original type
            fieldTypeMap[field.Name] = GetResolvedFieldType(field.FieldType, resolvedValue);
            memberOrder.Add((field.Name, field.FieldType, field));
        }

        // Create new dynamic type with resolved field types
        var newType = LinqRuntimeTypeBuilder.GetDynamicType(fieldTypeMap, originalType.Name);
        var newInstance = Activator.CreateInstance(newType);

        if (newInstance != null)
        {
            // Set field values on the new instance
            foreach (var kvp in fieldValues)
            {
                var newField = newType.GetField(kvp.Key);
                if (newField != null)
                {
                    newField.SetValue(newInstance, kvp.Value);
                }
            }
        }

        if (plan == null)
            TryCompileResolutionPlan(originalType, newType, memberOrder);

        return newInstance;
    }

    private static void TryCompileResolutionPlan(Type originalType, Type newType, List<(string Name, Type DeclaredType, MemberInfo Member)> memberOrder)
    {
        var objParam = Expression.Parameter(typeof(object), "obj");
        var valuesParam = Expression.Parameter(typeof(object?[]), "values");
        var typedObj = Expression.Convert(objParam, originalType);

        var getters = new Func<object, object?>[memberOrder.Count];
        var needsResolve = new bool[memberOrder.Count];
        var names = new string[memberOrder.Count];
        var targetTypes = new Type[memberOrder.Count];
        var bindings = new List<MemberBinding>(memberOrder.Count);

        for (var i = 0; i < memberOrder.Count; i++)
        {
            var (name, declaredType, member) = memberOrder[i];
            var targetField = newType.GetField(name);
            if (targetField == null)
                return; // unexpected shape - no plan, keep using the reflective path

            names[i] = name;
            needsResolve[i] = TypeCouldContainAsyncValue(declaredType);
            targetTypes[i] = targetField.FieldType;

            Expression access = member is PropertyInfo pi ? Expression.Property(typedObj, pi) : Expression.Field(typedObj, (FieldInfo)member);
            getters[i] = Expression.Lambda<Func<object, object?>>(Expression.Convert(access, typeof(object)), objParam).Compile();

            var item = Expression.ArrayIndex(valuesParam, Expression.Constant(i));
            // null resolved values become the field type's default (matching reflection SetValue(null) behavior)
            var assigned = Expression.Condition(Expression.Equal(item, Expression.Constant(null)), Expression.Default(targetField.FieldType), Expression.Convert(item, targetField.FieldType));
            bindings.Add(Expression.Bind(targetField, assigned));
        }

        var construct = Expression.Lambda<Func<object?[], object>>(Expression.Convert(Expression.MemberInit(Expression.New(newType), bindings), typeof(object)), valuesParam).Compile();

        resolutionPlans.TryAdd(originalType, new DynamicTypeResolutionPlan(names, getters, needsResolve, targetTypes, construct));
    }

    /// <summary>
    /// Gets the resolved field type - if the original type was Task<T>, returns T, otherwise returns the resolved value type
    /// </summary>
    private static Type GetResolvedFieldType(Type originalType, object? resolvedValue)
    {
        // If the original type was Task<T>, extract T
        if (typeof(Task).IsAssignableFrom(originalType) && originalType.IsGenericType)
        {
            var taskGenericType = originalType.GetGenericArguments().FirstOrDefault();
            if (taskGenericType != null)
                return taskGenericType;
        }
        // If the original type was ValueTask<T>, extract T
        if (originalType.IsGenericType && originalType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var vtGeneric = originalType.GetGenericArguments().FirstOrDefault();
            if (vtGeneric != null)
                return vtGeneric;
        }
        // If the original type was IAsyncEnumerable<T>, convert to IEnumerable<T>
        if (originalType.IsGenericType && originalType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
        {
            var t = originalType.GetGenericArguments()[0];
            return typeof(IEnumerable<>).MakeGenericType(t);
        }

        // Otherwise use the resolved value's type, or fall back to original type
        return resolvedValue?.GetType() ?? originalType;
    }

    private static bool ImplementsIAsyncEnumerable(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            return true;
        return type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
    }

    // materializer delegates cached per runtime type - the types are per-query-shape anonymous types so the
    // reflection (interface scan + MakeGenericMethod) runs once per shape, not per request
    private static readonly ConcurrentDictionary<Type, Func<object, CancellationToken, Task<object>>?> asyncListMaterializers = new();
    private static readonly ConcurrentDictionary<Type, Func<object, object>> syncListMaterializers = new();

    /// <summary>
    /// Materializes a deferred root list result. Async-capable LINQ providers (e.g. EF Core) return query
    /// objects that implement IAsyncEnumerable&lt;T&gt; - those are enumerated asynchronously so the database
    /// round-trip does not block a thread and the request's CancellationToken can cancel the in-flight query.
    /// Anything else (in-memory LINQ) is enumerated synchronously, matching the previous in-tree ToList()
    /// </summary>
    private static async Task<object> MaterializeDeferredResultAsync(object result, CancellationToken cancellationToken)
    {
        var type = result.GetType();
        var asyncMaterializer = asyncListMaterializers.GetOrAdd(
            type,
            static t =>
            {
                var asyncInterface = t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
                if (asyncInterface == null)
                    return null;
                var method = typeof(ExecutableGraphQLStatement).GetMethod(nameof(MaterializeAsyncEnumerable), BindingFlags.NonPublic | BindingFlags.Static)!;
                return (Func<object, CancellationToken, Task<object>>)
                    Delegate.CreateDelegate(typeof(Func<object, CancellationToken, Task<object>>), method.MakeGenericMethod(asyncInterface.GetGenericArguments()[0]));
            }
        );
        if (asyncMaterializer != null)
            return await asyncMaterializer(result, cancellationToken);

        var syncMaterializer = syncListMaterializers.GetOrAdd(
            type,
            static t =>
            {
                // find the element type via the IEnumerable<T> interface - the runtime type is a LINQ iterator
                // (e.g. SelectEnumerableIterator<TSource, TResult>) whose generic arguments don't map to the
                // element type directly. The List<T> element type must match the query's projected type exactly
                // as the second (services) pass reflects over it
                var enumerableInterface =
                    t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>) ? t : t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                if (enumerableInterface == null)
                    return static source => ((IEnumerable)source).Cast<object?>().ToList();
                var method = typeof(ExecutableGraphQLStatement).GetMethod(nameof(MaterializeEnumerable), BindingFlags.NonPublic | BindingFlags.Static)!;
                return (Func<object, object>)Delegate.CreateDelegate(typeof(Func<object, object>), method.MakeGenericMethod(enumerableInterface.GetGenericArguments()[0]));
            }
        );
        return syncMaterializer(result);
    }

    private static async Task<object> MaterializeAsyncEnumerable<T>(object source, CancellationToken cancellationToken)
    {
        // both branches build the same List<T> the previous in-tree ToList() produced - the element type is
        // typically a per-query anonymous type and the second (services) pass reflects over this exact type
#if NET10_0_OR_GREATER
        // in-box System.Linq.AsyncEnumerable - equivalent to the manual loop below but picks up any future
        // BCL optimisations. Not used on older TFMs to avoid a package dependency (and the extension-method
        // ambiguity it causes for consumers using the community System.Linq.Async package)
        return await ((IAsyncEnumerable<T>)source).ToListAsync(cancellationToken);
#else
        var list = new List<T>();
        var enumerator = ((IAsyncEnumerable<T>)source).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (await enumerator.MoveNextAsync())
                list.Add(enumerator.Current);
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
        return list;
#endif
    }

    private static List<T> MaterializeEnumerable<T>(object source) => ((IEnumerable<T>)source).ToList();

    internal static async Task<object> BufferAsyncEnumerable(object asyncEnumerableObj, CancellationToken cancellationToken = default)
    {
        // Use reflection to get the GetAsyncEnumerator method since we don't know the generic type at compile time
        var asyncEnumerableType = asyncEnumerableObj.GetType();

        // Get the element type from the original IAsyncEnumerable<T> first
        var elementType = typeof(object);
        var asyncEnumerableInterface = asyncEnumerableType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));

        if (asyncEnumerableInterface != null)
        {
            elementType = asyncEnumerableInterface.GetGenericArguments()[0];
        }

        // Create the properly typed list upfront
        var listType = typeof(List<>).MakeGenericType(elementType);
        var typedList = Activator.CreateInstance(listType)!;
        var addMethod = listType.GetMethod("Add")!;

        var getEnumeratorMethod = asyncEnumerableType.GetMethod("GetAsyncEnumerator", BindingFlags.Public | BindingFlags.Instance);

        if (getEnumeratorMethod == null)
        {
            // Try to find it on interfaces
            var interfaces = asyncEnumerableType.GetInterfaces();
            foreach (var iface in interfaces)
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                {
                    getEnumeratorMethod = iface.GetMethod("GetAsyncEnumerator");
                    break;
                }
            }
        }

        if (getEnumeratorMethod != null)
        {
            var enumerator = getEnumeratorMethod.Invoke(asyncEnumerableObj, [cancellationToken]);
            if (enumerator != null)
            {
                var enumeratorType = enumerator.GetType();

                // Find the IAsyncEnumerator<T> interface on the enumerator
                var asyncEnumeratorInterface = enumeratorType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAsyncEnumerator<>));

                MethodInfo? moveNextMethod = null;
                PropertyInfo? currentProperty = null;
                MethodInfo? disposeMethod = null;

                if (asyncEnumeratorInterface != null)
                {
                    // Get methods from the interface
                    moveNextMethod = asyncEnumeratorInterface.GetMethod("MoveNextAsync");
                    currentProperty = asyncEnumeratorInterface.GetProperty("Current");
                    disposeMethod = asyncEnumeratorInterface.GetMethod("DisposeAsync");
                }
                else
                {
                    // Fallback: try to get methods directly from the type
                    moveNextMethod = enumeratorType.GetMethod("MoveNextAsync");
                    currentProperty = enumeratorType.GetProperty("Current");
                    disposeMethod = enumeratorType.GetMethod("DisposeAsync");
                }

                if (moveNextMethod != null && currentProperty != null)
                {
                    try
                    {
                        while (true)
                        {
                            var moveNextTask = moveNextMethod.Invoke(enumerator, null);
                            if (moveNextTask is ValueTask<bool> valueTask)
                            {
                                var result = await valueTask;
                                if (!result)
                                    break;

                                var current = currentProperty.GetValue(enumerator);
                                if (current != null)
                                {
                                    var resolvedCurrent = await ResolveAsyncResultsRecursive(current, cancellationToken);
                                    addMethod.Invoke(typedList, [resolvedCurrent]);
                                }
                            }
                            else
                            {
                                break; // Unexpected return type
                            }
                        }
                    }
                    finally
                    {
                        if (disposeMethod != null)
                        {
                            var disposeTask = disposeMethod.Invoke(enumerator, null);
                            if (disposeTask is ValueTask disposeValueTask)
                                await disposeValueTask;
                        }
                    }
                }
            }
        }

        return typedList;
    }

    public IEnumerable<string> BuildPath()
    {
        if (Name == null)
            return [];
        return [Name];
    }
}
