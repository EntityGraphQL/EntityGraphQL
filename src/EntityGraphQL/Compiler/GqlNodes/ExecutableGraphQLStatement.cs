using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EntityGraphQL.Compiler.Util;
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
    protected Dictionary<string, ArgType> OpDefinedVariables { get; set; } = [];
    public ISchemaProvider Schema { get; protected set; }

    public ParameterExpression? OpVariableParameter { get; }

    public IField? Field { get; }
    public bool HasServices => Field?.Services.Count > 0;

    public IReadOnlyDictionary<string, object?> Arguments { get; }

    public string? Name { get; }

    public List<BaseGraphQLField> QueryFields { get; } = [];
    protected List<GraphQLDirective> Directives { get; } = [];

    /// <summary>
    /// This represents the operation type node which is not a root field
    /// </summary>
    public bool IsRootField => false;

    public ExecutableGraphQLStatement(ISchemaProvider schema, string? name, Expression nodeExpression, ParameterExpression rootParameter, Dictionary<string, ArgType> opVariables)
    {
        Name = name;
        NextFieldContext = nodeExpression;
        RootParameter = rootParameter;
        OpDefinedVariables = opVariables;
        this.Schema = schema;
        Arguments = new Dictionary<string, object?>();
        if (OpDefinedVariables.Count > 0)
        {
            // this type if all the variables defined in the GraphQL document
            // use PropertySetTracking to track is they are set or not (either by a default value or by the user in variables passed in)
            var variableType = LinqRuntimeTypeBuilder.GetDynamicType(OpDefinedVariables.ToDictionary(f => f.Key, f => f.Value.RawType), "docVars", typeof(ArgumentsTracker));
            OpVariableParameter = Expression.Parameter(variableType, "docVars");
        }
    }

    public virtual async Task<(ConcurrentDictionary<string, object?> data, IGraphQLValidator validator)> ExecuteAsync<TContext>(
        TContext? context,
        IServiceProvider? serviceProvider,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        Func<string, string> fieldNamer,
        ExecutionOptions options,
        QueryVariables? variables,
        QueryRequestContext requestContext,
        CancellationToken cancellationToken = default
    )
    {
        if (context == null && serviceProvider == null)
            throw new EntityGraphQLCompilerException("Either context or serviceProvider must be provided.");

        // build separate expression for all root level nodes in the op e.g. op is
        // query Op1 {
        //      people { name id }
        //      movies { released name }
        // }
        // people & movies will be the 2 fields that will be 2 separate expressions
        var result = new ConcurrentDictionary<string, object?>();
        var validator = new GraphQLValidator();

        IArgumentsTracker? docVariables = BuildDocumentVariables(ref variables);

        foreach (var fieldNode in QueryFields)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                var contextToUse = GetContextToUse(context, serviceProvider!, fieldNode)!;

                (var data, var didExecute) = await CompileAndExecuteNodeAsync(
                    new CompileContext(options, null, requestContext, cancellationToken),
                    contextToUse,
                    serviceProvider,
                    fragments,
                    fieldNode,
                    docVariables
                );
#if DEBUG
                if (options.IncludeDebugInfo)
                {
                    timer?.Stop();
                    result[$"__{fieldNode.Name}_timeMs"] = timer?.ElapsedMilliseconds;
                }
#endif

                // often use return null if mutation failed and added errors to validation
                // don't include it if it is not a nullable field
                if (data == null && fieldNode.Field?.ReturnType.TypeNotNullable == true)
                    continue;

                if (didExecute)
                    result[fieldNode.Name] = data;
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
                throw new EntityGraphQLFieldException(fieldNode.Name, null, ex);
            }
        }
        return (result, validator);
    }

    protected static TContext GetContextToUse<TContext>(TContext? context, IServiceProvider serviceProvider, BaseGraphQLField fieldNode)
    {
        if (context == null)
            return serviceProvider.GetService<TContext>()! ?? throw new EntityGraphQLCompilerException($"Could not find service of type {typeof(TContext).Name} to execute field {fieldNode.Name}");

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
                        argValue = ExpressionUtil.ConvertObjectType(variables.GetValueOrDefault(name) ?? argType.DefaultValue.Value, argType.RawType, Schema, null);
                        variablesToUse!.MarkAsSet(name);
                    }
                    if (argValue == null && argType.IsRequired)
                        throw new EntityGraphQLCompilerException(
                            $"Supplied variable '{name}' is null while the variable definition is non-null. Please update query document or supply a non-null value."
                        );
                    OpVariableParameter.Type.GetField(name)!.SetValue(variablesToUse, argValue);
                }
                catch (EntityGraphQLCompilerException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new EntityGraphQLCompilerException($"Supplied variable '{name}' can not be applied to defined variable type '{argType.Type}'", ex);
                }
            }
        }

        return variablesToUse;
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
        object? runningContext = context;

        var replacer = new ParameterReplacer();
        // For root/top level fields we need to first select the whole graph without fields that require services
        // so that EF Core can run and optimize the query against the DB
        // We then select the full graph from that context

        if (node.RootParameter == null)
            throw new EntityGraphQLCompilerException($"Root parameter not set for {node.Name}");

        Expression? expression = null;
        var contextParam = node.RootParameter;
        bool isSecondExec = false;

        if (node.HasServicesAtOrBelow(fragments) && compileContext.ExecutionOptions.ExecuteServiceFieldsSeparately)
        {
            // build this first as NodeExpression may modify ConstantParameters
            // this is without fields that require services
            expression = node.GetNodeExpression(compileContext, serviceProvider, fragments, OpVariableParameter, docVariables, contextParam, withoutServiceFields: true, null, null, false, replacer);
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
                compileContext = new(compileContext.ExecutionOptions, bulkData, compileContext.RequestContext, compileContext.CancellationToken);

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

#pragma warning disable IDE0074 // Use compound assignment
        if (expression == null)
        {
            // just do things normally
            expression = node.GetNodeExpression(compileContext, serviceProvider, fragments, OpVariableParameter, docVariables, contextParam, false, null, null, contextChanged: false, replacer);
        }
#pragma warning restore IDE0074 // Use compound assignment

        var data = await ExecuteExpressionAsync(expression, runningContext, contextParam, serviceProvider, replacer, compileContext, node, true, fragments, isSecondExec);
        return data;
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
                    serviceProvider!,
                    compileContext.Services,
                    allArgs,
                    bulkResolver.FieldExpression.Body,
                    parameters,
                    replacer,
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
            return await ConcurrencyLimitFieldExtension.ExecuteWithConcurrencyLimitAsync(
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
        // inject dependencies into the fullSelection
        if (serviceProvider != null)
        {
            expression = GraphQLHelper.InjectServices(serviceProvider, compileContext.Services, allArgs, expression, parameters, replacer, compileContext.CancellationToken);
        }

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

        var lambdaExpression = Expression.Lambda(expression, parameters.ToArray());

#if DEBUG
        if (compileContext.ExecutionOptions.NoExecution)
            return (null, false);
#endif
        object? res = null;
        if (lambdaExpression.ReturnType.IsGenericType && lambdaExpression.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            res = await (dynamic?)lambdaExpression.Compile().DynamicInvoke(allArgs.ToArray());
        else
            res = lambdaExpression.Compile().DynamicInvoke(allArgs.ToArray());

        // Resolve any nested async results in the returned object graph if the query contains async fields
        if (res != null && node.HasAsyncFieldsAtOrBelow(fragments) && (!compileContext.ExecutionOptions.ExecuteServiceFieldsSeparately || isSecondExec))
            res = await ResolveAsyncResultsRecursive(res, compileContext.CancellationToken);

        return (res, true);
    }

    public virtual void AddField(BaseGraphQLField field)
    {
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

            // Get the result from Task<T>
            var taskType = task.GetType();
            // Try to get Result property
            var resultProp = taskType.GetProperty(nameof(Task<object>.Result));
            if (resultProp != null)
            {
                var taskResult = resultProp.GetValue(task);
                return taskResult != null ? await ResolveAsyncResultsRecursive(taskResult, cancellationToken) : null;
            }

            return null; // Task (not Task<T>)
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

            return resolvedItems;
        }

        // Handle complex objects (including anonymous types and dynamic types)
        if (type.IsClass && type != typeof(string) && !type.IsPrimitive)
        {
            return await ResolveComplexObject(obj, type, cancellationToken);
        }

        return obj;
    }

    /// <summary>
    /// Handles complex objects including anonymous types, dynamic types, and regular classes
    /// </summary>
    private static async Task<object?> ResolveComplexObject(object obj, Type type, CancellationToken cancellationToken = default)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

        // For anonymous types and dynamically generated types, try to reconstruct the object
        if (IsAnonymousOrDynamicType(type))
        {
            // all types should be anonymous types built by LinqRuntimeTypeBuilder
            return await RebuildDynamicTypeWithResolvedFields(obj, type, properties, fields, cancellationToken);
        }

        // For regular mutable objects, we can modify in place
        foreach (var prop in properties.Where(p => p.CanRead && p.CanWrite))
        {
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
    /// Determines if a type is anonymous or dynamically generated (like those created by EntityGraphQL)
    /// </summary>
    private static bool IsAnonymousOrDynamicType(Type type)
    {
        return type.Namespace?.StartsWith("EntityGraphQL", StringComparison.Ordinal) == true || type.Assembly.IsDynamic;
    }

    /// <summary>
    /// Rebuilds a dynamic type with resolved field types (converting Task<T> fields to T fields)
    /// </summary>
    private static async Task<object?> RebuildDynamicTypeWithResolvedFields(object obj, Type originalType, PropertyInfo[] properties, FieldInfo[] fields, CancellationToken cancellationToken = default)
    {
        var fieldTypeMap = new Dictionary<string, Type>();
        var fieldValues = new Dictionary<string, object?>();

        // Process properties
        foreach (var prop in properties.Where(p => p.CanRead))
        {
            var value = prop.GetValue(obj);
            var resolvedValue = value != null ? await ResolveAsyncResultsRecursive(value, cancellationToken) : null;

            fieldValues[prop.Name] = resolvedValue;
            // If original type was Task<T>, use T. Otherwise use the resolved value type or original type
            fieldTypeMap[prop.Name] = GetResolvedFieldType(prop.PropertyType, resolvedValue);
        }

        // Process fields
        foreach (var field in fields)
        {
            var value = field.GetValue(obj);
            var resolvedValue = value != null ? await ResolveAsyncResultsRecursive(value, cancellationToken) : null;

            fieldValues[field.Name] = resolvedValue;
            // If original type was Task<T>, use T. Otherwise use the resolved value type or original type
            fieldTypeMap[field.Name] = GetResolvedFieldType(field.FieldType, resolvedValue);
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

        return newInstance;
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
}
