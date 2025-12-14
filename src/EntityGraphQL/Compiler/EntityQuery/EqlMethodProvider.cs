using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Compiler.EntityQuery;

/// <summary>
/// A unified method provider that registers all methods (default and custom) using a consistent registration system.
/// This replaces the need for separate DefaultMethodProvider and ExtensibleMethodProvider classes.
/// </summary>
public class EqlMethodProvider : IMethodProvider
{
    private readonly Dictionary<string, RegisteredMethodInfo> registeredMethods;
    private readonly HashSet<Type> isAnySupportedTypes = new();

    public EqlMethodProvider()
    {
        registeredMethods = new Dictionary<string, RegisteredMethodInfo>(StringComparer.OrdinalIgnoreCase);
        InitializeIsAnySupportedTypes();
        RegisterDefaultMethods();
    }

    #region Public Registration Methods

    public void RegisterMethods<T>() => RegisterMethods(typeof(T));

    public void RegisterMethods(Type extensionType)
    {
        ValidateNotNull(extensionType, nameof(extensionType));

        var methods = extensionType.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.GetCustomAttribute<EqlMethodAttribute>() != null);

        foreach (var method in methods)
        {
            try
            {
                RegisterMethod(method);
            }
            catch (InvalidOperationException)
            { /* Skip invalid methods */
            }
        }
    }

    public void RegisterMethod(MethodInfo method)
    {
        var parameters = method.GetParameters();
        RegisterMethod(method, parameters.Length > 0 ? parameters[0].ParameterType : typeof(object));
    }

    /// <summary>
    /// Registers a method for use in the language system.
    /// </summary>
    /// <param name="method">The MethodInfo used to call the method</param>
    /// <param name="methodContextType">The context type for the method. Limits the types that the method can be called on.</param>
    /// <param name="filterMethodName">An optional name to use for the method</param>
    /// <param name="extraArgs">Additional arguments to pass to the method call. Useful for methods like those in EF.Functions. These are prefixed.</param>
    public void RegisterMethod(MethodInfo method, Type methodContextType, string? filterMethodName = null, Expression[]? extraArgs = null)
    {
        var methodName = filterMethodName ?? GetCamelCaseMethodName(method.Name);
        ValidateMethodName(methodName);
        ValidateUniqueMethodName(methodName);

        var methodInfo = new RegisteredMethodInfo
        {
            Method = method,
            MethodContextType = methodContextType,
            MethodName = methodName,
            Origin = MethodOrigin.Custom,
            MakeCallFunc = (context, argContext, methodName, args) =>
            {
                var instance = method.IsStatic ? null : context;
                var allArgs = method.IsStatic ? [context, .. args] : args;
                if (extraArgs != null && extraArgs.Length > 0)
                    allArgs = [.. extraArgs, .. allArgs];
                var call = Expression.Call(instance, method, allArgs);
                return call;
            },
        };

        registeredMethods[methodName] = methodInfo;
    }

    public void RegisterMethod(Type methodContextType, string filterMethodName, Func<Expression, Expression, string, Expression[], Expression> makeCallFunc)
    {
        RegisterMethodInternal(methodContextType, filterMethodName, makeCallFunc, MethodOrigin.Custom);
    }

    public void RegisterMethod(Func<Type, bool> typePredicate, string filterMethodName, Func<Expression, Expression, string, Expression[], Expression> makeCallFunc)
    {
        RegisterMethodInternal(typeof(object), filterMethodName, makeCallFunc, MethodOrigin.Custom, typePredicate);
    }

    private void RegisterMethodInternal(
        Type methodContextType,
        string filterMethodName,
        Func<Expression, Expression, string, Expression[], Expression> makeCallFunc,
        MethodOrigin origin,
        Func<Type, bool>? typePredicate = null
    )
    {
        ValidateMethodName(filterMethodName);
        ValidateUniqueMethodName(filterMethodName);

        var methodInfo = new RegisteredMethodInfo
        {
            Method = null!,
            MethodContextType = methodContextType,
            MethodName = filterMethodName,
            MakeCallFunc = makeCallFunc,
            Origin = origin,
            TypePredicate = typePredicate,
        };

        registeredMethods[filterMethodName] = methodInfo;
    }

    private void RegisterMethodWithTypePredicate(Func<Type, bool> typePredicate, string filterMethodName, Func<Expression, Expression, string, Expression[], Expression> makeCallFunc)
    {
        RegisterMethodInternal(typeof(object), filterMethodName, makeCallFunc, MethodOrigin.Default, typePredicate);
    }

    #endregion

    #region Query Methods

    public IReadOnlyCollection<RegisteredMethodInfo> GetRegisteredMethods() => registeredMethods.Values.ToList().AsReadOnly();

    public IReadOnlyCollection<RegisteredMethodInfo> GetCustomRegisteredMethods() => registeredMethods.Values.Where(m => m.Origin == MethodOrigin.Custom).ToList().AsReadOnly();

    public void ClearAllMethods() => registeredMethods.Clear();

    public void ClearCustomMethods()
    {
        var customKeys = registeredMethods.Where(kvp => kvp.Value.Origin == MethodOrigin.Custom).Select(kvp => kvp.Key).ToList();

        foreach (var key in customKeys)
            registeredMethods.Remove(key);
    }

    public bool EntityTypeHasMethod(Type context, string methodName) => registeredMethods.TryGetValue(methodName, out var method) && IsTypeCompatible(context, method);

    public Expression GetMethodContext(Expression context, string methodName) => DefaultMethodImplementations.GetContextFromEnumerable(context);

    public Expression MakeCall(Expression context, Expression argContext, string methodName, IEnumerable<Expression>? args, Type type)
    {
        if (!registeredMethods.TryGetValue(methodName, out var method))
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Unsupported method {methodName}");

        if (!IsTypeCompatible(type, method))
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Method '{methodName}' cannot be called on type '{type}'. Expected '{method.MethodContextType}'");

        if (method.MakeCallFunc != null)
            return method.MakeCallFunc(context, argContext, method.MethodName, args?.ToArray() ?? Array.Empty<Expression>());
        throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Method '{methodName}' does not have a MakeCallFunc defined");
    }

    #endregion

    #region Default Method Registration

    private void RegisterDefaultMethods()
    {
        // String methods
        RegisterMethodInternal(typeof(string), "contains", DefaultMethodImplementations.MakeStringContainsMethod, MethodOrigin.Default);
        RegisterMethodInternal(typeof(string), "startsWith", DefaultMethodImplementations.MakeStringStartsWithMethod, MethodOrigin.Default);
        RegisterMethodInternal(typeof(string), "endsWith", DefaultMethodImplementations.MakeStringEndsWithMethod, MethodOrigin.Default);
        RegisterMethodInternal(typeof(string), "toLower", DefaultMethodImplementations.MakeStringToLowerMethod, MethodOrigin.Default);
        RegisterMethodInternal(typeof(string), "toUpper", DefaultMethodImplementations.MakeStringToUpperMethod, MethodOrigin.Default);

        // Enumerable methods - these need special type predicates for efficiency
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "where", DefaultMethodImplementations.MakeWhereMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "filter", DefaultMethodImplementations.MakeWhereMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "first", DefaultMethodImplementations.MakeFirstMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "firstOrDefault", DefaultMethodImplementations.MakeFirstOrDefaultMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "last", DefaultMethodImplementations.MakeLastMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "lastOrDefault", DefaultMethodImplementations.MakeLastOrDefaultMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "take", DefaultMethodImplementations.MakeTakeMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "skip", DefaultMethodImplementations.MakeSkipMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "count", DefaultMethodImplementations.MakeCountMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "any", DefaultMethodImplementations.MakeAnyMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "all", DefaultMethodImplementations.MakeAllMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "orderby", DefaultMethodImplementations.MakeOrderByMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "orderByDesc", DefaultMethodImplementations.MakeOrderByDescMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "sum", DefaultMethodImplementations.MakeSumMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "min", DefaultMethodImplementations.MakeMinMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "max", DefaultMethodImplementations.MakeMaxMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "average", DefaultMethodImplementations.MakeAverageMethod);
        RegisterMethodWithTypePredicate(t => t.IsEnumerableOrArray(), "selectMany", DefaultMethodImplementations.MakeSelectManyMethod);

        // IsAny method with complex type predicate
        RegisterMethodWithTypePredicate(CreateIsAnyTypePredicate(), "isAny", DefaultMethodImplementations.MakeIsAnyMethod);
    }

    private void InitializeIsAnySupportedTypes()
    {
        isAnySupportedTypes.Clear();
        var defaults = new[]
        {
            typeof(string),
            typeof(long),
            typeof(long?),
            typeof(int),
            typeof(int?),
            typeof(short),
            typeof(short?),
            typeof(byte),
            typeof(byte?),
            typeof(double),
            typeof(double?),
            typeof(float),
            typeof(float?),
            typeof(decimal),
            typeof(decimal?),
            typeof(uint),
            typeof(uint?),
            typeof(ulong),
            typeof(ulong?),
            typeof(ushort),
            typeof(ushort?),
            typeof(sbyte),
            typeof(sbyte?),
            typeof(char),
            typeof(char?),
            typeof(DateTime),
            typeof(DateTime?),
            typeof(Guid),
            typeof(Guid?),
            typeof(DateTimeOffset),
            typeof(DateTimeOffset?),
            typeof(TimeSpan),
            typeof(TimeSpan?)
#if NET8_0_OR_GREATER
            ,
            typeof(DateOnly),
            typeof(DateOnly?),
            typeof(TimeOnly),
            typeof(TimeOnly?)
#endif
        };
        foreach (var t in defaults)
        {
            isAnySupportedTypes.Add(t);
        }
    }

    internal void ExtendIsAnySupportedTypes(params Type[] types)
    {
        foreach (var t in types)
        {
            // Always add the provided type
            isAnySupportedTypes.Add(t);

            // If a nullable form is provided, also add its underlying type (non-nullable)
            var underlying = Nullable.GetUnderlyingType(t);
            if (underlying != null)
            {
                isAnySupportedTypes.Add(underlying);
                continue;
            }

            // If a non-nullable value type is provided, also add its nullable variant
            if (t.IsValueType)
            {
                var nullable = typeof(Nullable<>).MakeGenericType(t);
                isAnySupportedTypes.Add(nullable);
            }
        }
    }

    private Func<Type, bool> CreateIsAnyTypePredicate()
    {
        return t => isAnySupportedTypes.Contains(t);
    }

    #endregion

    #region Validation Helpers

    private static void ValidateNotNull(object? value, string paramName)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value);
#else
        if (value == null)
            throw new ArgumentNullException(paramName);
#endif
    }

    private static void ValidateMethodName(string methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName))
            throw new ArgumentException("Filter method name cannot be empty");
    }

    private void ValidateUniqueMethodName(string methodName)
    {
        if (registeredMethods.ContainsKey(methodName))
            throw new InvalidOperationException($"A method with name '{methodName}' is already registered");
    }

    private static bool IsTypeCompatible(Type contextType, RegisteredMethodInfo methodInfo)
    {
        return methodInfo.TypePredicate?.Invoke(contextType) ?? methodInfo.MethodContextType.IsAssignableFrom(contextType);
    }

    private static string GetCamelCaseMethodName(string methodName) => methodName.Length > 0 ? char.ToLowerInvariant(methodName[0]) + methodName.Substring(1) : methodName;

    #endregion
}
