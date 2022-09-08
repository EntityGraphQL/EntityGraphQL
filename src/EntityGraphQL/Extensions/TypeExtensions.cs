using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

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

        /// <summary>
        /// Returns true if this type is an Expression<>
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool IsExpression(this Type source)
        {
            if (!source.IsGenericType)
                return false;
            return source.GetGenericTypeDefinition() == typeof(Expression<>);            
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

        /// <summary>
        /// Return the array element type or the generic type for a IEnumerable<T>
        /// Specifically does not treat string as IEnumerable<char> and will not return byte for byte[]
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static LambdaExpression? GetExpression(this Type type, LambdaExpression le)
        {
            var instance = Activator.CreateInstance(type);
            return le.Compile().DynamicInvoke(instance) as LambdaExpression;
        }

        public static bool IsNullableType(this Type t)
        {
            return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
        }


        public static bool IsNullable(this MemberInfo memberInfo)
        {
            if (memberInfo is PropertyInfo property)
            {
                return IsNullableHelper(property.PropertyType, property.DeclaringType, property.CustomAttributes);
            }

            if (memberInfo is FieldInfo fieldInfo)
            {
                return IsNullableHelper(fieldInfo.FieldType, fieldInfo.DeclaringType, fieldInfo.CustomAttributes);
            }

            if (memberInfo is MethodInfo methodInfo)
            {
                return IsNullableHelper(
                    methodInfo.GetActualReturnType(),
                    methodInfo,
                    methodInfo.ReturnParameter.CustomAttributes
                );
            }

            return true;
        }

        public static bool IsNullable(this ParameterInfo parameterInfo)
        {
            return IsNullableHelper(parameterInfo.ParameterType, parameterInfo.Member, parameterInfo.CustomAttributes);
        }

        private static Type GetActualReturnType(this MethodInfo methodInfo)
        {
            for (var type = methodInfo.ReturnType; type != null; type = type.GetGenericArguments().Last())
            {
                if (!type.IsGenericType)
                {
                    return type;
                }
            }

            throw new Exception("Could not figure out return type");
        }

        private static bool IsNullableHelper(Type memberType, MemberInfo? declaringType, IEnumerable<CustomAttributeData> customAttributes)
        {
            if (memberType.IsValueType)
            {
                return Nullable.GetUnderlyingType(memberType) != null;
            }

            var nullable = customAttributes
                .FirstOrDefault(x => x.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
            if (nullable != null && nullable.ConstructorArguments.Count == 1)
            {
                var attributeArgument = nullable.ConstructorArguments[0];
                if (attributeArgument.ArgumentType == typeof(byte[]))
                {
                    var args = (ReadOnlyCollection<CustomAttributeTypedArgument>)attributeArgument.Value!;
                    if (args.Count > 0 && args.Last().ArgumentType == typeof(byte))
                    {
                        return (byte)args.Last().Value! == 2;
                    }
                }
                else if (attributeArgument.ArgumentType == typeof(byte))
                {
                    return (byte)attributeArgument.Value! == 2;
                }
            }

            for (var type = declaringType; type != null; type = type.DeclaringType)
            {
                var context = type.CustomAttributes
                    .FirstOrDefault(x => x.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute");
                if (context != null &&
                    context.ConstructorArguments.Count == 1 &&
                    context.ConstructorArguments[0].ArgumentType == typeof(byte))
                {
                    return (byte)context.ConstructorArguments[0].Value! == 2;
                }
            }

            // Couldn't find a suitable attribute
            return true;
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

            if (type.BaseType.IsGenericType && type.BaseType.GetGenericTypeDefinition() == genericType)
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
