using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
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

    // We build a key based on all the selected fields so we can cache the anonymous types we built.
    // The key must be a full, collision-free description (field names + assembly-qualified field types +
    // parent type) - a hash key could collide and hand a query a type with the wrong fields/parent.
    // Type names can't be > 1024 length, so the CLR type name itself uses a Guid instead of this key.
    private static readonly Dictionary<string, Type> builtTypes = [];

    private static string GetTypeKey(IReadOnlyDictionary<string, Type> fields, Type? parentType)
    {
        var key = new StringBuilder();
        foreach (var field in fields.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            key.Append(field.Key).Append(':').Append(field.Value.AssemblyQualifiedName ?? field.Value.ToString()).Append(';');
        }
        if (parentType != null)
            key.Append('|').Append(parentType.AssemblyQualifiedName ?? parentType.ToString());
        return key.ToString();
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

        var typeKey = GetTypeKey(fields, parentType);
        lock (lockObj)
        {
            if (builtTypes.TryGetValue(typeKey, out var existingType))
            {
                return existingType;
            }

            var className = $"{DynamicTypePrefix}{description}_{Guid.NewGuid()}";
            var typeBuilder = moduleBuilder.DefineType(className, TypeAttributes.Public | TypeAttributes.Class, parentType);

            foreach (var field in fields)
            {
                if (parentType != null && parentType.GetField(field.Key) != null)
                    continue;

                typeBuilder.DefineField(field.Key, field.Value, FieldAttributes.Public);
            }

            var newType = typeBuilder.CreateTypeInfo()!.AsType();
            builtTypes[typeKey] = newType;
            return newType;
        }
    }
}
