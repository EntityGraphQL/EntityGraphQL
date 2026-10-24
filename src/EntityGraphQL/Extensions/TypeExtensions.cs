using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EntityGraphQL.Extensions;

public static class TypeExtensions
{
    /// <summary>
    /// Return the Type unwrapped from any Nullable<>
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public static Type GetNonNullableType(this Type source)
    {
        if (source.IsNullableType())
        {
            return source.GetGenericArguments()[0];
        }
        return source;
    }

    /// <summary>
    /// Return the Type wrapped as a Nullable<>
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public static Type GetNullableType(this Type source)
    {
        var type = Nullable.GetUnderlyingType(source) ?? source;
        if (type.IsValueType)
            return typeof(Nullable<>).MakeGenericType(type);
        else
            return type;
    }

    /// <summary>
    /// The element type of a list, or the type itself without Nullable&lt;&gt;. A dictionary is the type itself, not its
    /// key/value pairs - it is looked up as a scalar or a property bag.
    /// </summary>
    public static Type GetNonNullableOrEnumerableType(this Type source)
    {
        var type = source.GetNonNullableType();
        return type.IsDictionary() ? type : type.GetEnumerableOrArrayType() ?? type;
    }

    public static bool IsDictionary(this Type source)
    {
        return IsGenericTypeDictionary(source);
    }

    private static bool IsGenericTypeDictionary(Type source)
    {
        var isDictionary = source.IsGenericType && source.GetGenericTypeDefinition() == typeof(IDictionary<,>);
        if (isDictionary)
            return isDictionary;

        foreach (var intType in source.GetInterfaces())
        {
            isDictionary = IsGenericTypeDictionary(intType);
            if (isDictionary)
                break;
        }
        return isDictionary;
    }

    /// <summary>
    /// Returns true if this type is an Enumerable<> or an array
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public static bool IsEnumerableOrArray(this Type source)
    {
        if (source == typeof(string) || source == typeof(byte[]))
            return false;

        if (source.IsArray)
            return true;

        var isEnumerable = false;
        if (source.IsGenericType && !source.IsNullableType())
            isEnumerable = IsGenericTypeEnumerable(source);

        return isEnumerable;
    }

    public static bool IsGenericTypeEnumerable(this Type source)
    {
        bool isEnumerable = source.IsGenericType && source.GetGenericTypeDefinition() == typeof(IEnumerable<>) || source.IsGenericType && source.GetGenericTypeDefinition() == typeof(IQueryable<>);
        if (isEnumerable)
            return isEnumerable;

        foreach (var intType in source.GetInterfaces())
        {
            isEnumerable = IsGenericTypeEnumerable(intType);
            if (isEnumerable)
                break;
        }

        return isEnumerable;
    }

    public static bool IsAsyncGenericType(this Type source)
    {
        return source.IsGenericType
            && (source.GetGenericTypeDefinition() == typeof(Task<>) || source.GetGenericTypeDefinition() == typeof(ValueTask<>) || source.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
    }

    /// <summary>
    /// Returns true if the type is <c>Task&lt;T&gt;</c> or <c>ValueTask&lt;T&gt;</c> — i.e. directly awaitable.
    /// Use this instead of <see cref="IsAsyncGenericType"/> when you need to unwrap the async wrapper
    /// to reach the inner type (e.g. to find the enumerable element type), because
    /// <c>IAsyncEnumerable&lt;T&gt;</c> is NOT a wrapper: its generic argument already IS the element type.
    /// </summary>
    public static bool IsAwaitableGenericType(this Type source)
    {
        return source.IsGenericType && (source.GetGenericTypeDefinition() == typeof(Task<>) || source.GetGenericTypeDefinition() == typeof(ValueTask<>));
    }

    public static bool IsGenericTypeQueryable(this Type source)
    {
        // Handle Task<> or other generic wrappers potentially containing IQueryable<>
        if (source.IsGenericType)
        {
            var genericDef = source.GetGenericTypeDefinition();

            if (genericDef.IsAsyncGenericType())
            {
                var innerType = source.GetGenericArguments()[0];
                return IsGenericTypeQueryable(innerType);
            }
        }

        // Check if source is directly IQueryable<>
        if (source.IsGenericType && source.GetGenericTypeDefinition() == typeof(IQueryable<>))
            return true;

        // Recursively check all interfaces to find IQueryable<>
        foreach (var intType in source.GetInterfaces())
        {
            if (IsGenericTypeQueryable(intType))
                return true;
        }

        return false;
    }

    /// <summary>
    /// The element type of a collection, by the IEnumerable&lt;T&gt; (or IAsyncEnumerable&lt;T&gt;) it implements: a class
    /// Bags : List&lt;Bag&gt; is a collection of Bag, a dictionary one of its key/value pairs, and a Task&lt;T&gt;,
    /// Nullable&lt;T&gt; or OffsetPage&lt;T&gt; no collection at all. Does not treat string as IEnumerable&lt;char&gt;
    /// and will not return byte for byte[]
    /// </summary>
    public static Type? GetEnumerableOrArrayType(this Type type)
    {
        if (type == typeof(string) || type == typeof(byte[]))
            return null;
        if (type.IsArray)
            return type.GetElementType();
        // IEnumerable<T>, or IAsyncEnumerable<T> - a stream of T is a collection of T too
        static bool IsCollection(Type t) => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(IEnumerable<>) || t.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
        if (IsCollection(type))
            return type.GetGenericArguments()[0];
        var enumerable = type.GetInterfaces().FirstOrDefault(IsCollection);
        return enumerable?.GetGenericArguments()[0];
    }

    public static bool IsNullableType(this Type t)
    {
        return Nullable.GetUnderlyingType(t) != null;
    }

    public static bool ImplementsGenericInterface(this Type type, Type genericInterfaceType)
    {
        // Deal with the edge case
        if (type.IsGenericType && type.GetGenericTypeDefinition() == genericInterfaceType)
            return true;

        foreach (var inter in type.GetInterfaces())
        {
            var implements = ImplementsGenericInterface(inter, genericInterfaceType);
            if (implements)
                return implements;
        }
        return false;
    }

    public static Type? GetGenericArgument(this Type type, Type genericType)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == genericType)
            return type.GetGenericArguments()[0];

        if (type.BaseType?.IsGenericType == true && type.BaseType.GetGenericTypeDefinition() == genericType)
            return type.GetGenericArguments()[0];

        foreach (var inter in type.GetInterfaces())
        {
            if (inter.IsGenericType && inter.GetGenericTypeDefinition() == genericType)
                return inter.GetGenericArguments()[0];

            var genericArg = GetGenericArgument(inter, genericType);
            if (genericArg != null)
                return genericArg;
        }
        return null;
    }
}
