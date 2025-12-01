using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
#if NET9_0_OR_GREATER
using System.Threading;
#endif

namespace EntityGraphQL.Compiler.Util;

/// <summary>
/// Builds .NET types at runtime and caches them to be reused
/// </summary>
public static class LinqRuntimeTypeBuilder
{
    public static readonly string DynamicAssemblyName = "EntityGraphQL.DynamicTypes";
    public static readonly string DynamicTypePrefix = "Dynamic_";
    private static readonly AssemblyName assemblyName = new() { Name = DynamicAssemblyName };
    private static readonly ModuleBuilder moduleBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run).DefineDynamicModule(assemblyName.Name);

#if NET9_0_OR_GREATER
    private static readonly Lock lockObj = new();
#else
    private static readonly object lockObj = new();
#endif

    // We build a key based on all the selected fields so we can cache the anonymous types we built
    // Type names can't be > 1024 length, so we store them against a shorter Guid string
    // Key: concatenated field names + field types
    // Value: (ClassName, Type)
    private static readonly Dictionary<int, (string ClassName, Type Type)> typesByFullName = [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetTypeKey(IReadOnlyDictionary<string, Type> fields, Type? parentType)
    {
        var hash = new HashCode();
        foreach (var field in fields.OrderBy(f => f.Key))
        {
            hash.Add(field.Key);
            hash.Add(field.Value);
        }
        if (parentType != null)
            hash.Add(parentType.Name);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Build a dynamic type based on the fields. Types are cached so they only are created once
    /// </summary>
    /// <param name="fields">Field names and the type of the field.</param>
    /// <param name="description">An optional description string. Helps with debugging - e.g. the field the type is built for</param>
    /// <param name="parentType">If the type inherits from another type</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static Type GetDynamicType(IReadOnlyDictionary<string, Type> fields, string description, Type? parentType = null)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(fields, nameof(fields));
#else
        if (null == fields)
            throw new ArgumentNullException(nameof(fields));
#endif

        var typeHashCode = GetTypeKey(fields, parentType);
        lock (lockObj)
        {
            if (typesByFullName.TryGetValue(typeHashCode, out var typeInfo))
            {
                return typeInfo.Type;
            }

            var className = $"{DynamicTypePrefix}{description}_{Guid.NewGuid()}";
            var typeBuilder = moduleBuilder.DefineType(className, TypeAttributes.Public | TypeAttributes.Class, parentType);

            foreach (var field in fields)
            {
                if (parentType != null && parentType.GetField(field.Key) != null)
                    continue;

                typeBuilder.DefineField(field.Key, field.Value, FieldAttributes.Public);
            }

            typesByFullName[typeHashCode] = (className, typeBuilder.CreateTypeInfo()!.AsType());
            return typesByFullName[typeHashCode].Type;
        }
    }
}
