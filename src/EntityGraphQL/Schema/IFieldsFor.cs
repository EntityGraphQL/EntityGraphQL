namespace EntityGraphQL.Schema;

/// <summary>
/// Marker interface tying a field-grouping class (used with AddFieldsFrom&lt;T&gt;() / AddQueryFieldsFrom&lt;T&gt;())
/// to the context type its [GraphQLField] methods are written against. Adding the class to a different schema
/// type fails at compile time instead of at schema build. It also gives compile-time tooling (analyzers) the
/// intended context type so field methods can be checked statically.
/// Contravariant so a class written for a base type can be added to a type deriving from it.
/// </summary>
/// <typeparam name="TContext">The dotnet type of the schema type the fields are for - the entity type for
/// schema.Type&lt;Person&gt;().AddFieldsFrom&lt;T&gt;(), the query context for AddQueryFieldsFrom&lt;T&gt;()</typeparam>
#pragma warning disable CA1040 // marker interface by design - it exists purely to carry the context type
public interface IFieldsFor<in TContext> { }
#pragma warning restore CA1040
