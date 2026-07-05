using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions;

/// <summary>
/// Field extension that wraps async field expressions with concurrency control
/// Uses expression wrapping pattern similar to WrapObjectProjectionFieldForNullCheck
/// </summary>
public class ConcurrencyLimitFieldExtension : BaseFieldExtension
{
    private readonly int? maxConcurrency;
    private readonly List<Type>? serviceTypes;

    /// <summary>
    /// Compiled form of each wrapped operation lambda, keyed by expression instance. For a list field the
    /// wrapped call executes per element with the same lambda instance - without this it compiled per element.
    /// Weakly keyed so entries die with their expression trees.
    /// </summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<LambdaExpression, Delegate> compiledOperations = new();

    /// <summary>
    /// Create a service-based concurrency limit (resolved from ExecutionOptions)
    /// </summary>
    /// <param name="serviceTypes">The service type to apply limits to</param>
    /// <param name="maxConcurrency">Optional override for the service limit</param>
    public ConcurrencyLimitFieldExtension(IEnumerable<Type>? serviceTypes, int? maxConcurrency = null)
    {
        this.serviceTypes = serviceTypes?.ToList();
        this.maxConcurrency = maxConcurrency;
    }

    public override (Expression? expression, ParameterExpression? originalArgParam, ParameterExpression? newArgParam, object? argumentValue) GetExpressionAndArguments(
        IField field,
        FieldExtensionExpressionContext context
    )
    {
        var expression = context.Expression;
        var argumentParam = context.ArgumentParameter;
        var arguments = context.Arguments;
        var servicesPass = context.ServicesPass;
        var originalArgParam = context.OriginalArgumentParameter;
        var compileContext = context.CompileContext;

        // Only apply concurrency control during the service pass for async expressions
        if (!servicesPass || !IsAsyncExpression(expression))
        {
            return (expression, originalArgParam, argumentParam, arguments);
        }

        // Generate the semaphore configurations for hierarchical limiting
        var semaphoreConfigs = GetSemaphoreConfigs(field, compileContext.ExecutionOptions);

        // Skip if no concurrency limits are configured
        if (semaphoreConfigs.Count == 0)
        {
            return (expression, originalArgParam, argumentParam, arguments);
        }

        // The limiter registry and CancellationToken are per-request state. They must be passed into the
        // expression as parameters (values supplied at execution time via the compile context's constant
        // parameters) - baking them in as Expression.Constant would freeze request 1's registry and token
        // into a delegate reused by later requests when CacheCompiledDelegates is on
        var registryParam = Expression.Parameter(typeof(ConcurrencyLimiterRegistry), "concurrencyRegistry");
        var cancellationTokenParam = Expression.Parameter(typeof(CancellationToken), "concurrencyCancelToken");
        compileContext.AddConstant(null, registryParam, compileContext.ConcurrencyLimiterRegistry);
        compileContext.AddConstant(null, cancellationTokenParam, compileContext.CancellationToken);

        // Wrap the async expression with hierarchical concurrency control
        var newExp = WrapAsyncExpressionWithConcurrencyLimit(field, expression, semaphoreConfigs, registryParam, cancellationTokenParam);
        return (newExp, originalArgParam, argumentParam, arguments);
    }

    private static bool IsAsyncExpression(Expression expression) => expression.Type.IsAsyncGenericType();

    /// <summary>
    /// Wraps an async field expression with hierarchical concurrency limiting
    /// Similar to ExpressionUtil.WrapObjectProjectionFieldForNullCheck
    /// </summary>
    private static MethodCallExpression WrapAsyncExpressionWithConcurrencyLimit(
        IField field,
        Expression asyncExpression,
        List<(string scopeKey, int maxConcurrency)> semaphoreConfigs,
        Expression concurrencyLimiterRegistry,
        Expression cancellationToken
    )
    {
        List<ParameterExpression> expArgs = [field.FieldParam!, .. field.Services];
        Expression asyncExpressionExp = Expression.Lambda(asyncExpression, expArgs);

        // Create an array expression containing the dynamic arguments
        var expArgsArray = Expression.NewArrayInit(typeof(object), expArgs.Cast<Expression>());

        // Convert semaphore configs to a constant expression
        var semaphoreConfigsConstant = Expression.Constant(semaphoreConfigs);

        Expression[] arguments = [asyncExpressionExp, semaphoreConfigsConstant, concurrencyLimiterRegistry, expArgsArray, cancellationToken];

        var call = Expression.Call(typeof(ConcurrencyLimitFieldExtension), nameof(ExecuteWithConcurrencyLimitAsync), null, arguments);
        return call;
    }

    private List<(string scopeKey, int maxConcurrency)> GetSemaphoreConfigs(IField field, ExecutionOptions executionOptions)
    {
        var configs = new List<(string scopeKey, int maxConcurrency)>();

        // 1. Global query limit (if specified)
        var globalLimit = executionOptions.MaxQueryConcurrency ?? 0;
        if (globalLimit > 0)
        {
            configs.Add(("global_query", globalLimit));
        }

        // 2. Service-specific limit (if specified)
        if (serviceTypes != null)
        {
            foreach (var serviceType in serviceTypes)
            {
                var serviceLimit = executionOptions.ServiceConcurrencyLimits.GetValueOrDefault(serviceType);
                if (serviceLimit > 0)
                {
                    configs.Add(($"service_{serviceType.AssemblyQualifiedName}", serviceLimit));
                }
            }
        }

        // 3. Field-specific limit (if specified)
        if (maxConcurrency.HasValue)
        {
            configs.Add(($"field_{field.FromType.Name}.{field.Name}_{maxConcurrency}", maxConcurrency.Value));
        }

        return configs;
    }

    /// <summary>
    /// Runtime execution method that applies hierarchical concurrency limiting to async operations
    /// This gets called during expression execution, not compilation
    /// </summary>
    public static async Task<object?> ExecuteWithConcurrencyLimitAsync(
        LambdaExpression asyncOperationExp,
        List<(string scopeKey, int maxConcurrency)> semaphoreConfigs,
        ConcurrencyLimiterRegistry concurrencyLimiterRegistry,
        object[] expArgs,
        CancellationToken cancellationToken
    )
    {
        // Get all semaphores for hierarchical limiting
        var semaphores = semaphoreConfigs.Select(config => concurrencyLimiterRegistry.GetSemaphore(config.scopeKey, config.maxConcurrency)).ToList();

        // Acquire all semaphores in order (query -> service -> field). Acquisition happens inside the
        // try so a cancelled/faulted WaitAsync releases the permits already acquired
        var acquired = 0;
        try
        {
            foreach (var semaphore in semaphores)
            {
                await semaphore.WaitAsync(cancellationToken);
                acquired++;
            }

            // Execute the async operation. The lambda is compiled once per expression instance - this method
            // runs per row for list fields, so compiling here every call is expensive
            var compiledOperation = compiledOperations.GetValue(asyncOperationExp, static exp => exp.Compile());
            var asyncOperation = compiledOperation.DynamicInvoke(expArgs);
            if (asyncOperation is null)
            {
                return null;
            }
            if (asyncOperation is Task task)
            {
                await task;

                // Get the result from Task<T>
                var taskType = task.GetType();
                if (taskType.IsGenericType)
                {
                    var resultProperty = taskType.GetProperty(nameof(Task<object>.Result));
                    return resultProperty?.GetValue(task);
                }
                return null;
            }
            // Handle ValueTask<T>
            var type = asyncOperation.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                var asTaskMethod = type.GetMethod(nameof(ValueTask<object>.AsTask));
                if (asTaskMethod != null)
                {
                    var taskToAwait = (Task?)asTaskMethod.Invoke(asyncOperation, null);
                    if (taskToAwait != null)
                    {
                        await taskToAwait;
                        var resultProperty = taskToAwait.GetType().GetProperty(nameof(ValueTask<object>.Result));
                        return resultProperty?.GetValue(taskToAwait);
                    }
                }
            }

            return asyncOperation; // Not async, return as-is
        }
        finally
        {
            // Release the acquired semaphores in reverse order (field -> service -> query)
            for (int i = acquired - 1; i >= 0; i--)
            {
                semaphores[i].Release();
            }
        }
    }
}
