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
        BaseGraphQLField fieldNode,
        Expression expression,
        ParameterExpression? argumentParam,
        dynamic? arguments,
        Expression context,
        bool servicesPass,
        ParameterReplacer parameterReplacer,
        ParameterExpression? originalArgParam,
        CompileContext compileContext
    )
    {
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

        // Wrap the async expression with hierarchical concurrency control
        var newExp = WrapAsyncExpressionWithConcurrencyLimit(field, expression, semaphoreConfigs, compileContext.ConcurrencyLimiterRegistry, compileContext.CancellationToken);
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
        ConcurrencyLimiterRegistry concurrencyLimiterRegistry,
        CancellationToken cancellationToken
    )
    {
        List<ParameterExpression> expArgs = [field.FieldParam!, .. field.Services];
        Expression asyncExpressionExp = Expression.Lambda(asyncExpression, expArgs);

        // Create an array expression containing the dynamic arguments
        var expArgsArray = Expression.NewArrayInit(typeof(object), expArgs.Cast<Expression>());

        // Convert semaphore configs to a constant expression
        var semaphoreConfigsConstant = Expression.Constant(semaphoreConfigs);

        Expression[] arguments = [asyncExpressionExp, semaphoreConfigsConstant, Expression.Constant(concurrencyLimiterRegistry), expArgsArray, Expression.Constant(cancellationToken)];

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

        // Acquire all semaphores in order (query -> service -> field)
        foreach (var semaphore in semaphores)
        {
            await semaphore.WaitAsync(cancellationToken);
        }

        try
        {
            // Execute the async operation
            var asyncOperation = asyncOperationExp.Compile().DynamicInvoke(expArgs);
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
            // Release all semaphores in reverse order (field -> service -> query)
            for (int i = semaphores.Count - 1; i >= 0; i--)
            {
                semaphores[i].Release();
            }
        }
    }
}
