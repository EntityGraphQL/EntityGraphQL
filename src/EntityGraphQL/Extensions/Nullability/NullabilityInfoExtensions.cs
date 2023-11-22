using System.Collections.Concurrent;
using System;
using System.Reflection;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;

namespace Nullability
{
    /// <summary>
    /// Static and thread safe wrapper around <see cref="NullabilityInfoContext"/>.
    /// </summary>
    public static class NullabilityInfoExtensions
    {
        private static readonly ConcurrentDictionary<ParameterInfo, NullabilityInfo> parameterCache = new();
        private static readonly ConcurrentDictionary<PropertyInfo, NullabilityInfo> propertyCache = new();
        private static readonly ConcurrentDictionary<EventInfo, NullabilityInfo> eventCache = new();
        private static readonly ConcurrentDictionary<FieldInfo, NullabilityInfo> fieldCache = new();

        public static NullabilityInfo GetNullabilityInfo(this MemberInfo info)
        {
            if (info is PropertyInfo propertyInfo)
            {
                return propertyInfo.GetNullabilityInfo();
            }

            if (info is EventInfo eventInfo)
            {
                return eventInfo.GetNullabilityInfo();
            }

            if (info is FieldInfo fieldInfo)
            {
                return fieldInfo.GetNullabilityInfo();
            }

            if (info is MethodInfo methodInfo)
            {
                return methodInfo.GetNullabilityInfo();
            }

            throw new ArgumentException($"Unsupported type:{info.GetType().FullName}");
        }

        public static NullabilityInfo Unwrap(this NullabilityInfo info)
        {
            if (info.GenericTypeArguments.Length == 0)
            {
                return info;
            }

            if (info.Type.GetGenericTypeDefinition() == typeof(Expression<>))
            {
                return info.GenericTypeArguments[0].Unwrap();
            }

            if (info.Type.Name.StartsWith("Func`", StringComparison.InvariantCulture))
            {
                return info.GenericTypeArguments[^1].Unwrap();
            }

            return info;
        }

        public static NullabilityInfo GetNullabilityInfo(this MethodInfo info)
        {
            return info.ReturnParameter.GetNullabilityInfo();
        }

        public static NullabilityState GetNullability(this MemberInfo info)
        {
            return GetReadOrWriteState(info.Name, info.GetNullabilityInfo());
        }

        public static bool IsNullable(this MemberInfo info)
        {
            var nullability = info.GetNullabilityInfo();
            return IsNullable(info.Name, nullability);
        }

        public static NullabilityInfo GetNullabilityInfo(this FieldInfo info)
        {
            return fieldCache.GetOrAdd(info, inner =>
            {
                var nullabilityContext = new NullabilityInfoContext();
                return nullabilityContext.Create(inner);
            });
        }

        public static NullabilityState GetNullability(this FieldInfo info)
        {
            return GetReadOrWriteState(info.Name, info.GetNullabilityInfo());
        }

        public static bool IsNullable(this FieldInfo info)
        {
            var nullability = info.GetNullabilityInfo();
            return IsNullable(info.Name, nullability);
        }

        public static NullabilityInfo GetNullabilityInfo(this EventInfo info)
        {
            return eventCache.GetOrAdd(info, inner =>
            {
                var nullabilityContext = new NullabilityInfoContext();
                return nullabilityContext.Create(inner);
            });
        }

        public static NullabilityState GetNullability(this EventInfo info)
        {
            return GetReadOrWriteState(info.Name, info.GetNullabilityInfo());
        }

        public static bool IsNullable(this EventInfo info)
        {
            var nullability = info.GetNullabilityInfo();
            return IsNullable(info.Name, nullability);
        }

        public static NullabilityInfo GetNullabilityInfo(this PropertyInfo info)
        {
            return propertyCache.GetOrAdd(info, inner =>
            {
                var nullabilityContext = new NullabilityInfoContext();
                return nullabilityContext.Create(inner);
            });
        }

        public static NullabilityState GetNullability(this PropertyInfo info)
        {
            return GetReadOrWriteState(info.Name, info.GetNullabilityInfo());
        }

        public static bool IsNullable(this PropertyInfo info)
        {
            var nullability = info.GetNullabilityInfo();
            return IsNullable(info.Name, nullability);
        }

        public static NullabilityInfo GetNullabilityInfo(this ParameterInfo info)
        {
            return parameterCache.GetOrAdd(info, inner =>
            {
                var nullabilityContext = new NullabilityInfoContext();
                return nullabilityContext.Create(inner);
            });
        }

        public static NullabilityState GetNullability(this ParameterInfo info)
        {
            return GetReadOrWriteState(info.Name!, info.GetNullabilityInfo());
        }

        public static bool IsNullable(this ParameterInfo info)
        {
            var nullability = info.GetNullabilityInfo();
            return IsNullable(info.Name!, nullability);
        }

        static NullabilityState GetReadOrWriteState(string name, NullabilityInfo nullability)
        {
            if (nullability.ReadState != NullabilityState.Unknown)
            {
                return nullability.ReadState;
            }

            return nullability.WriteState;
        }

        static NullabilityState GetKnownState(string name, NullabilityInfo nullability)
        {
            var readState = nullability.ReadState;
            if (readState != NullabilityState.Unknown)
            {
                return readState;
            }

            var writeState = nullability.WriteState;
            if (writeState != NullabilityState.Unknown)
            {
                return writeState;
            }

            throw new EntityGraphQLCompilerException($"The nullability of '{nullability.Type.FullName}.{name}' is unknown. Assembly: {nullability.Type.Assembly.FullName}.");
        }

        static bool IsNullable(string name, NullabilityInfo nullability)
        {
            return GetKnownState(name, nullability) == NullabilityState.Nullable;
        }

        //Patching
        public static MemberInfo GetMemberWithSameMetadataDefinitionAs(Type type, MemberInfo member)
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            foreach (var info in type.GetMembers(all))
            {
                if (info.HasSameMetadataDefinitionAs(member))
                {
                    return info;
                }
            }

            throw new MissingMemberException(type.FullName, member.Name);
        }

        //https://github.com/dotnet/runtime/issues/23493
        public static bool IsGenericMethodParameter(this Type target)
        {
            return target.IsGenericParameter &&
                   target.DeclaringMethod != null;
        }
    }
}