using System;
using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL.Schema.FieldExtensions;

/// <summary>
/// Schema types an extension adds for the fields it is on - pages, edges, connections, aggregates - found
/// by name, not by dotnet type: schema types that share a dotnet type (property bags) share OffsetPage&lt;T&gt;
/// and the rest too, but each needs its own.
/// </summary>
internal static class GeneratedTypes
{
    /// <summary>
    /// The type of this name: added and built, or the one already added for another field over the same
    /// items. A type of that name that is not of this dotnet type (the schema's own PersonPage, say) is an
    /// error, not one to reuse.
    /// </summary>
    public static ISchemaType GetOrAdd(ISchemaProvider schema, string name, Type dotnetType, string description, Action<ISchemaType> build)
    {
        if (Existing(schema, name, dotnetType) is { } existing)
            return existing;
        var added = schema.AddType(dotnetType, name, description);
        build(added);
        return added;
    }

    /// <summary>The type of this name if it has been added already - and is of this dotnet type, else an error.</summary>
    public static ISchemaType? Existing(ISchemaProvider schema, string name, Type dotnetType) => Existing(schema, name, t => t == dotnetType, dotnetType.Name);

    /// <summary>
    /// The type of this name if it has been added already and its dotnet type is one to reuse; a type of that
    /// name that is not (the schema's own PersonPage or PersonAggregate, say) is an error.
    /// </summary>
    public static ISchemaType? Existing(ISchemaProvider schema, string name, Func<Type, bool> reusable, string neededFor)
    {
        if (!schema.HasType(name))
            return null;
        var existing = schema.Type(name);
        if (!reusable(existing.TypeDotnet))
            throw new EntityGraphQLSchemaException($"A type named '{name}' is needed for {neededFor}, but the schema already has a '{name}' of {existing.TypeDotnet.Name}. Rename that type.");
        return existing;
    }

    /// <summary>
    /// All the fields of a generated type, its one member of the items built with the items' schema type rather
    /// than by its dotnet type - which can not say which for property bags.
    /// </summary>
    public static void AddAllFieldsWithItems(ISchemaType type, string member, ISchemaType itemsSchemaType) =>
        type.AddFields(SchemaBuilder.GetFieldsFromObject(type.TypeDotnet, type, type.Schema, new SchemaBuilderOptions(), false, (member, itemsSchemaType)));

    /// <summary>
    /// The items' schema type of a collection field: from paging or an aggregate where they wrap the field's,
    /// else the field's own.
    /// </summary>
    public static ISchemaType ItemsSchemaType(IField field) =>
        field.Extensions.OfType<IWrapsItemsExtension>().Select(e => e.OriginalSchemaType).FirstOrDefault(t => t != null) ?? field.ReturnType.SchemaType;
}
