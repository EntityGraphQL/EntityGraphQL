using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using EntityGraphQL.Compiler;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema;

public class SubscriptionField : MethodField
{
    public override GraphQLQueryFieldType FieldType { get; } = GraphQLQueryFieldType.Subscription;

    /// <summary>
    /// True when the method is declared as returning Task&lt;IObservable&lt;T&gt;&gt; or ValueTask&lt;IObservable&lt;T&gt;&gt;
    /// rather than IObservable&lt;T&gt; directly.
    /// </summary>
    private readonly bool isObservableAsync;

    public SubscriptionField(
        ISchemaProvider schema,
        ISchemaType fromType,
        string methodName,
        GqlTypeInfo returnType,
        MethodInfo method,
        string description,
        RequiredAuthorization? requiredAuth,
        bool isAsync,
        SchemaBuilderOptions options
    )
        : base(
            schema,
            fromType,
            methodName,
            returnType,
            method,
            description,
            requiredAuth,
            // Pass isAsync=false for observable-async methods: we handle the Task unwrapping
            // ourselves in CallAsync to avoid a DLR bug with 'await (dynamic?)Task<IObservable<T>>'.
            isAsync && !method.ReturnType.IsAwaitableGenericType(),
            options
        )
    {
        // Unwrap Task<> / ValueTask<> before checking for IObservable<> so that
        // async subscription methods (Task<IObservable<T>> / ValueTask<IObservable<T>>) are accepted.
        var rawReturnType = method.ReturnType;
        isObservableAsync = rawReturnType.IsAwaitableGenericType();
        if (isObservableAsync)
            rawReturnType = rawReturnType.GetGenericArguments()[0];
        if (!rawReturnType.ImplementsGenericInterface(typeof(IObservable<>)))
            throw new EntityGraphQLSchemaException($"Subscription {methodName} should return IObservable<T>, Task<IObservable<T>>, or ValueTask<IObservable<T>>");
    }

    public override async Task<(object? data, IGraphQLValidator? methodValidator)> CallAsync(
        object? context,
        IReadOnlyDictionary<string, object?>? gqlRequestArgs,
        IServiceProvider? serviceProvider,
        ParameterExpression? variableParameter,
        IArgumentsTracker? docVariables,
        CompileContext compileContext
    )
    {
        var (result, validator) = await base.CallAsync(context, gqlRequestArgs, serviceProvider, variableParameter, docVariables, compileContext);

        if (!isObservableAsync)
            return (result, validator);

        // result is Task<IObservable<T>> or ValueTask<IObservable<T>> boxed as object.
        // base.CallAsync used the sync path (IsAsync=false), so it returned the raw Task/ValueTask.
        // `await (dynamic?)task` fails at runtime with RuntimeBinderException
        // ("Cannot implicitly convert type 'void' to 'object'") because the DLR resolves
        // GetResult() via the non-generic Task base class instead of Task<IObservable<T>>.
        // Unwrap explicitly using reflection instead.
        if (result is Task task)
        {
            await task.ConfigureAwait(false);
            var resultProp = task.GetType().GetProperty(nameof(Task<object>.Result));
            return (resultProp?.GetValue(task), validator);
        }

        return (result, validator);
    }
}
