using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using EntityGraphQL.Schema;
#if NET9_0_OR_GREATER
using System.Threading;
#endif

namespace EntityGraphQL.Compiler.Util;

/// <summary>
/// Builds .NET types at runtime and caches them to be reused. One builder per schema, its types in a collectible
/// assembly: once the schema is no longer referenced its types can be unloaded, so an app that creates and drops
/// schemas (one per tenant, or per version of a runtime-defined model) does not grow forever.
/// </summary>
internal sealed class LinqRuntimeTypeBuilder
{
    public static readonly string DynamicAssemblyName = "EntityGraphQL.DynamicTypes";
    public static readonly string DynamicTypePrefix = "Dynamic_";

    private static readonly ConditionalWeakTable<ISchemaProvider, LinqRuntimeTypeBuilder> bySchema = new();

    // the builder of each type built - a type rebuilt later (e.g. with its async fields resolved) goes in the same
    // builder, as nothing that lives longer may reference it
    private static readonly ConditionalWeakTable<Type, LinqRuntimeTypeBuilder> byType = new();

    /// <summary>
    /// For the obsolete public methods that are not given a schema. Never unloaded, as all types were before.
    /// </summary>
    internal static readonly LinqRuntimeTypeBuilder Shared = new();

    /// <summary>The builder of a schema's types</summary>
    public static LinqRuntimeTypeBuilder For(ISchemaProvider schema) => bySchema.GetValue(schema, static _ => new LinqRuntimeTypeBuilder());

    /// <summary>The builder that built this type, or the shared one if none of them did</summary>
    public static LinqRuntimeTypeBuilder For(Type type) => byType.TryGetValue(type, out var builder) ? builder : Shared;

    private readonly ModuleBuilder moduleBuilder;

#if NET9_0_OR_GREATER
    private readonly Lock lockObj = new();
#else
    private readonly object lockObj = new();
#endif

    // We build a key based on all the selected fields so we can cache the anonymous types we built.
    // The key must be a full, collision-free description (field names + assembly-qualified field types +
    // parent type) - a hash key could collide and hand a query a type with the wrong fields/parent.
    // Type names can't be > 1024 length, so the CLR type name itself uses a Guid instead of this key.
    private readonly Dictionary<string, Type> builtTypes = [];

    private LinqRuntimeTypeBuilder()
    {
        var assemblyName = new AssemblyName { Name = DynamicAssemblyName };
        moduleBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndCollect).DefineDynamicModule(assemblyName.Name!);
    }

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
    public Type GetDynamicType(IReadOnlyDictionary<string, Type> fields, string description, Type? parentType = null)
    {
        ArgumentNullException.ThrowIfNull(fields, nameof(fields));

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
            byType.AddOrUpdate(newType, this);
            return newType;
        }
    }
}
