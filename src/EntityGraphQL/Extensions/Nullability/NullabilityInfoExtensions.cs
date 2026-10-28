using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nullability;

/// <summary>
/// Static and thread safe wrapper around <see cref="NullabilityInfoContext"/>.
/// </summary>
public static class NullabilityInfoExtensions
{
    private static readonly ConditionalWeakTable<ParameterInfo, NullabilityInfo> parameterCache = new();
    private static readonly ConditionalWeakTable<PropertyInfo, NullabilityInfo> propertyCache = new();
    private static readonly ConditionalWeakTable<EventInfo, NullabilityInfo> eventCache = new();
    private static readonly ConditionalWeakTable<FieldInfo, NullabilityInfo> fieldCache = new();

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

    public static NullabilityInfo GetNullabilityInfo(this FieldInfo info)
    {
        return fieldCache.GetValue(
            info,
            inner =>
            {
                var nullabilityContext = new NullabilityInfoContext();
                return nullabilityContext.Create(inner);
            }
        );
    }

    public static NullabilityInfo GetNullabilityInfo(this EventInfo info)
    {
        return eventCache.GetValue(
            info,
            inner =>
            {
                var nullabilityContext = new NullabilityInfoContext();
                return nullabilityContext.Create(inner);
            }
        );
    }

    public static NullabilityInfo GetNullabilityInfo(this PropertyInfo info)
    {
        return propertyCache.GetValue(
            info,
            inner =>
            {
                var nullabilityContext = new NullabilityInfoContext();
                return nullabilityContext.Create(inner);
            }
        );
    }

    public static NullabilityInfo GetNullabilityInfo(this ParameterInfo info)
    {
        return parameterCache.GetValue(
            info,
            inner =>
            {
                var nullabilityContext = new NullabilityInfoContext();
                return nullabilityContext.Create(inner);
            }
        );
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
        return target.IsGenericParameter && target.DeclaringMethod != null;
    }
}
