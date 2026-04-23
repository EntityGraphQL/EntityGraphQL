using System;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EntityGraphQL.AspNet;

public static class EntityGraphQLAspNetServiceCollectionExtensions
{
    private static SchemaProvider<TSchemaContext> BuildSchema<TSchemaContext>(IServiceProvider serviceProvider, AddGraphQLOptions<TSchemaContext> options)
    {
        var authService = serviceProvider.GetService<IAuthorizationService>();
        var webHostEnvironment = serviceProvider.GetService<IWebHostEnvironment>();
        var schemaOptions = options.Schema;
        var authorizationService = schemaOptions.AuthorizationService ?? (authService != null ? new PolicyOrRoleBasedAuthorization(authService) : null);
        var isDevelopment = schemaOptions.IsDevelopment;

        // Preserve the existing ASP.NET behavior: non-Development environments default to production-safe behavior.
        if (webHostEnvironment != null && !webHostEnvironment.IsEnvironment("Development"))
            isDevelopment = false;

        var schema = new SchemaProvider<TSchemaContext>(authorizationService, schemaOptions.FieldNamer, introspectionEnabled: schemaOptions.IntrospectionEnabled, isDevelopment: isDevelopment);

        foreach (var allowedException in schemaOptions.AllowedExceptions)
        {
            if (!schema.AllowedExceptions.Contains(allowedException))
                schema.AllowedExceptions.Add(allowedException);
        }

        options.Builder.PreBuildSchemaFromContext?.Invoke(schema);
        if (options.AutoBuildSchemaFromContext)
            schema.PopulateFromContext(options.Builder);
        options.GetConfigureSchema()?.Invoke(schema, serviceProvider);

        return schema;
    }

    /// <summary>
    /// Adds a SchemaProvider&lt;TSchemaContext&gt; as a singleton to the service collection.
    /// </summary>
    public static GraphQLSchemaBuilder<TSchemaContext> AddGraphQLSchema<TSchemaContext>(this IServiceCollection serviceCollection, Action<SchemaProvider<TSchemaContext>>? configure = null)
    {
        var builder = serviceCollection.AddGraphQLSchema((Action<AddGraphQLOptions<TSchemaContext>>)(_ => { }));
        if (configure != null)
            builder.ConfigureGraphQLSchema(configure);
        return builder;
    }

    /// <summary>
    /// Adds a SchemaProvider&lt;TSchemaContext&gt; as a singleton to the service collection.
    /// </summary>
    public static GraphQLSchemaBuilder<TSchemaContext> AddGraphQLSchema<TSchemaContext>(this IServiceCollection serviceCollection, Action<AddGraphQLOptions<TSchemaContext>> configure)
    {
        serviceCollection.TryAddSingleton<IGraphQLRequestDeserializer>(new DefaultGraphQLRequestDeserializer());
        serviceCollection.TryAddSingleton<IGraphQLResponseSerializer>(new DefaultGraphQLResponseSerializer());

        var options = new AddGraphQLOptions<TSchemaContext>(null);
        configure(options);

        serviceCollection.Add(new ServiceDescriptor(typeof(SchemaProvider<TSchemaContext>), sp => BuildSchema(sp, options), options.SchemaLifetime));

        return new GraphQLSchemaBuilder<TSchemaContext>(serviceCollection, options);
    }

    /// <summary>
    /// Configure schema-level concerns such as adding types, fields, directives, or authorization rules.
    /// </summary>
    public static GraphQLSchemaBuilder<TSchemaContext> ConfigureGraphQLSchema<TSchemaContext>(this GraphQLSchemaBuilder<TSchemaContext> builder, Action<SchemaProvider<TSchemaContext>> configure)
    {
        builder.Options.ConfigureSchema(configure);
        return builder;
    }

    /// <summary>
    /// Configure schema-level concerns with access to the active <see cref="IServiceProvider"/> used to build the schema.
    /// </summary>
    public static GraphQLSchemaBuilder<TSchemaContext> ConfigureGraphQLSchema<TSchemaContext>(
        this GraphQLSchemaBuilder<TSchemaContext> builder,
        Action<SchemaProvider<TSchemaContext>, IServiceProvider> configure
    )
    {
        builder.Options.ConfigureSchema(configure);
        return builder;
    }

    public static GraphQLSchemaBuilder<TSchemaContext> AddGraphQLValidator<TSchemaContext>(this GraphQLSchemaBuilder<TSchemaContext> builder)
    {
        builder.Services.AddGraphQLValidator();
        return builder;
    }

    /// <summary>
    /// Registers the default IGraphQLValidator implementation to use as a service in your method fields to report a collection of errors
    /// </summary>
    public static IServiceCollection AddGraphQLValidator(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddTransient<IGraphQLValidator, GraphQLValidator>();
        return serviceCollection;
    }
}
