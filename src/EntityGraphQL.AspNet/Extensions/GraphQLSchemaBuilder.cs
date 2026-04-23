using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.AspNet;

/// <summary>
/// Fluent builder returned from <see cref="EntityGraphQLAspNetServiceCollectionExtensions.AddGraphQLSchema{TSchemaContext}(IServiceCollection, System.Action{AddGraphQLOptions{TSchemaContext}})"/>.
/// </summary>
public class GraphQLSchemaBuilder<TSchemaContext>
{
    internal GraphQLSchemaBuilder(IServiceCollection services, AddGraphQLOptions<TSchemaContext> options)
    {
        Services = services;
        Options = options;
    }

    public IServiceCollection Services { get; }

    internal AddGraphQLOptions<TSchemaContext> Options { get; }
}
