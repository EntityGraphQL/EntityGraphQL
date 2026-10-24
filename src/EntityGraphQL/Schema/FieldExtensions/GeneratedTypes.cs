using System;

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
    /// items. A type of that name whose dotnet type is not one to reuse (the schema's own PersonPage or
    /// PersonAggregate, say) is an error.
    /// </summary>
    /// <param name="reusable">Whether an existing type of this name, by its dotnet type, is one this generated</param>
    /// <param name="neededFor">What the type is for, for the error</param>
    /// <param name="dotnetType">The new type's dotnet type - only made when it is added</param>
    public static ISchemaType GetOrAdd(
        ISchemaProvider schema,
        string name,
        Func<Type, bool> reusable,
        string neededFor,
        Func<Type> dotnetType,
        string description,
        Action<ISchemaType> build
    )
    {
        if (schema.HasType(name))
        {
            var existing = schema.Type(name);
            if (!reusable(existing.TypeDotnet))
                throw new EntityGraphQLSchemaException($"A type named '{name}' is needed for {neededFor}, but the schema already has a '{name}' of {existing.TypeDotnet.Name}. Rename that type.");
            return existing;
        }
        var added = schema.AddType(dotnetType(), name, description);
        build(added);
        return added;
    }

    /// <summary>
    /// All the fields of a generated type, its one member of the items built with the items' schema type rather
    /// than by its dotnet type - which can not say which for property bags.
    /// </summary>
    public static void AddAllFieldsWithItems(ISchemaType type, string member, ISchemaType itemsSchemaType) =>
        type.AddFields(SchemaBuilder.GetFieldsFromObject(type.TypeDotnet, type, type.Schema, new SchemaBuilderOptions(), false, (member, itemsSchemaType)));
}
