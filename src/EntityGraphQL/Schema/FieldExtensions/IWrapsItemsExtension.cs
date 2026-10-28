using System.Linq;
using System.Linq.Expressions;

namespace EntityGraphQL.Schema.FieldExtensions;

/// <summary>
/// An extension that replaces a collection field's schema type with a wrapper - a page, a connection,
/// items with an aggregate - and so has to say what the items' schema type was. With several schema
/// types sharing one dotnet type (property bags), the items' dotnet type can not.
/// </summary>
internal interface IWrapsItemsExtension
{
    /// <summary>The items' schema type: the field's own, before the wrapper replaced it.</summary>
    ISchemaType? OriginalSchemaType { get; }
}

/// <summary>A paging extension: the collection it pages, as well as its items' schema type.</summary>
internal interface IPagingExtension : IWrapsItemsExtension
{
    Expression? OriginalFieldExpression { get; }
}

internal static class WrapsItemsFieldExtensions
{
    /// <summary>
    /// The items' schema type of a collection field: from paging or an aggregate where they wrap the field's,
    /// else the field's own.
    /// </summary>
    public static ISchemaType ItemsSchemaType(this IField field) =>
        field.Extensions.OfType<IWrapsItemsExtension>().Select(e => e.OriginalSchemaType).FirstOrDefault(t => t != null) ?? field.ReturnType.SchemaType;
}
