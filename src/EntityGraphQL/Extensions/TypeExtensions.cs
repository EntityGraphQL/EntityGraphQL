using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace EntityGraphQL.Extensions
{
    public static class TypeExtensions
    {
        /// <summary>
        /// Returns true if this type is an Enumerable<> or an array
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool IsEnumerableOrArray(this Type source)
        {
            if (source == typeof(string) || source == typeof(byte[]))
                return false;

            if (source.GetTypeInfo().IsArray)
            {
                return true;
            }
            var isEnumerable = false;
            if (source.GetTypeInfo().IsGenericType)
            {
                isEnumerable = IsGenericTypeEnumerable(source);
            }
            return isEnumerable;
        }

        private static bool IsGenericTypeEnumerable(Type source)
        {
            bool isEnumerable = (source.GetGenericTypeDefinition() == typeof(IEnumerable<>) || source.GetGenericTypeDefinition() == typeof(IQueryable<>));
            if (!isEnumerable)
            {
                foreach (var intType in source.GetInterfaces())
                {
                    isEnumerable = IsGenericTypeEnumerable(intType);
                    if (isEnumerable)
                        break;
                }
            }

            return isEnumerable;
        }

        /// <summary>
        /// Return the arary element type or the generic type for a IEnumerable<T>
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Type GetEnumerableOrArrayType(this Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }
            if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];
            foreach (var intType in type.GetInterfaces())
            {
                if (intType.IsEnumerableOrArray())
                {
                    return intType.GetGenericArguments()[0];
                }
                var deepIntType = intType.GetEnumerableOrArrayType();
                if (deepIntType != null)
                    return deepIntType.GetGenericArguments()[0];
            }
            return null;
        }

        public static bool IsNullableType(this Type t)
        {
            return t.GetTypeInfo().IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
    }
}
