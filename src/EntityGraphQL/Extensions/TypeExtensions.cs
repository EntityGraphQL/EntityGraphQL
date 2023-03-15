using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Collections.ObjectModel;
using Nullability;

namespace EntityGraphQL.Extensions
{
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

        public static Type GetNonNullableOrEnumerableType(this Type source)
        {
            return source.GetNonNullableType().GetEnumerableOrArrayType() ?? source.GetNonNullableType();
        }

        public static bool IsDictionary(this Type source)
        {
            return IsGenericTypeDictionary(source);
        }

        private static bool IsGenericTypeDictionary(Type source)
        {
            var isDictionary = source.IsGenericType && source.GetGenericTypeDefinition() == typeof(IDictionary<,>);
            if (isDictionary) return isDictionary;

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
            if (isEnumerable) return isEnumerable;

            foreach (var intType in source.GetInterfaces())
            {
                isEnumerable = IsGenericTypeEnumerable(intType);
                if (isEnumerable)
                    break;
            }

            return isEnumerable;
        }

        /// <summary>
        /// Return the array element type or the generic type for a IEnumerable<T>
        /// Specifically does not treat string as IEnumerable<char> and will not return byte for byte[]
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Type? GetEnumerableOrArrayType(this Type type)
        {
            if (type == typeof(string) || type == typeof(byte[]) || type == typeof(byte))
            {
                return null;
            }
            if (type.IsArray)
                return type.GetElementType();
            if (type.GenericTypeArguments.Length == 1)
                return type.GetGenericArguments()[0];
            return null;
        }

        public static bool IsNullableType(this Type t)
        {
            return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
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
}
