using System.Collections.Generic;

namespace EntityGraphQL.Schema;

/// <summary>
/// The fields the engine will read off each object a resolver returns, so a resolver can fetch only those -
/// projecting a database query, or forwarding a selection to another service, instead of fetching everything.
///
/// This is not the caller's selection set verbatim, and the difference matters:
/// <list type="bullet">
/// <item>a selected field that is itself resolved from a service is not something this resolver can load, so
/// it is replaced by the dependency the engine reads to resolve it later (its bulk key, or whatever its
/// expression reads off the object) - fetch those or that field has nothing to work from</item>
/// <item>a field whose expression reads several members contributes all of them</item>
/// <item>meta fields like <c>__typename</c> contribute nothing - they are computed, never read</item>
/// <item>aliases collapse onto the field they select, and the same field selected in more than one place (or
/// through fragments) contributes the union of what those places read - one bulk load serves them all</item>
/// </list>
/// Take a parameter of this type in a resolver and the engine supplies it, in the same way it supplies
/// <c>CancellationToken</c> and <c>QueryRequestContext</c>.
/// </summary>
public interface IFieldSelection
{
    /// <summary>
    /// The fields read directly off the returned object. Nested objects the engine reads through carry their
    /// own <see cref="ISelectedField.Fields"/>.
    /// </summary>
    IReadOnlyList<ISelectedField> Fields { get; }

    /// <summary>
    /// Every field flattened to a dotted path - <c>id</c>, <c>name</c>, <c>address.city</c> - in the order
    /// they were selected. Convenient for projection helpers that take paths.
    /// </summary>
    IReadOnlyList<string> Paths { get; }

    /// <summary>True if <paramref name="path"/> is one of <see cref="Paths"/>, case insensitively.</summary>
    bool Contains(string path);
}

/// <summary>
/// One field the engine reads off a returned object. <see cref="Fields"/> is empty unless the engine reads
/// through it into a nested object.
/// </summary>
public interface ISelectedField
{
    /// <summary>The name of the member read off the object.</summary>
    string Name { get; }

    /// <summary>
    /// The schema field this came from, where there is one. Null for a dependency of a service field (that
    /// member is read to resolve another field, not selected in its own right) and for a bulk resolver's key.
    /// </summary>
    IField? SchemaField { get; }

    /// <summary>Fields read off this one, when the engine reads through it. Empty otherwise.</summary>
    IReadOnlyList<ISelectedField> Fields { get; }
}
