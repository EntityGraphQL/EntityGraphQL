using System;
using EntityGraphQL.Schema;

namespace EntityGraphQL.AspNet;

/// <summary>
/// Options for configuring GraphQL schema setup in ASP.NET applications.
/// Uses composition to provide access to both reflection and provider options.
/// </summary>
/// <typeparam name="TSchemaContext">The type of the schema context</typeparam>
public class AddGraphQLOptions<TSchemaContext>
{
    /// <summary>
    /// Options that control how SchemaBuilder reflects the object graph to auto-create schema types and fields.
    /// </summary>
    public SchemaBuilderOptions Builder { get; } = new();

    /// <summary>
    /// Options for configuring the SchemaProvider instance (authorization, introspection, error handling, field naming).
    /// </summary>
    public SchemaProviderOptions Schema { get; } = new();

    /// <summary>
    /// If true (default) the schema will be built via SchemaBuilder on the context type.
    /// You can customise this with the Builder property options.
    /// If false the schema will be created with the TSchemaContext as its context but will be empty of fields/types.
    /// You can fully populate it in the ConfigureSchema callback
    /// </summary>
    public bool AutoBuildSchemaFromContext { get; set; } = true;

    /// <summary>
    /// Called after the schema object is created but before the context is reflected into it (SchemaBuilder).
    /// Use for set up of type mappings or anything that may be needed for the schema to be built correctly.
    /// </summary>
    public Action<SchemaProvider<TSchemaContext>>? PreBuildSchemaFromContext { get; set; }

    /// <summary>
    /// Called after the context has been reflected into a schema to allow further customisation.
    /// Or use this to configure the whole schema if AutoBuildSchemaFromContext is false.
    /// </summary>
    public Action<SchemaProvider<TSchemaContext>>? ConfigureSchema { get; set; }
}
