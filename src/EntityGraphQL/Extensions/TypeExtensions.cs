using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace EntityGraphQL.Extensions
{
    public static class TypeExtensions
    {
        public static bool IsEnumerable(this Type source)
        {
            if (source == typeof(string) || source == typeof(byte[]))
                return false;

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

        public static Type GetEnumerableType(this Type type)
        {
            if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return type.GetGenericArguments()[0];
            foreach (var intType in type.GetInterfaces())
            {
                if (intType.IsEnumerable())
                {
                    return intType.GetGenericArguments()[0];
                }
                var deepIntType = intType.GetEnumerableType();
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
