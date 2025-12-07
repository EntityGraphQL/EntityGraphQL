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
    /// <summary>
    /// Adds a SchemaProvider&lt;TSchemaContext&gt; as a singleton to the service collection.
    /// </summary>
    /// <param name="serviceCollection"></param>
    /// <param name="configure">Function to further configure your schema</param>
    /// <typeparam name="TSchemaContext"></typeparam>
    /// <returns></returns>
    public static IServiceCollection AddGraphQLSchema<TSchemaContext>(this IServiceCollection serviceCollection, Action<SchemaProvider<TSchemaContext>>? configure = null)
    {
        serviceCollection.AddGraphQLSchema<TSchemaContext>(options =>
        {
            options.ConfigureSchema = configure;
        });

        return serviceCollection;
    }

    /// <summary>
    /// Adds a SchemaProvider&lt;TSchemaContext&gt; as a singleton to the service collection.
    /// </summary>
    /// <typeparam name="TSchemaContext">Context type to build the schema on</typeparam>
    /// <param name="serviceCollection"></param>
    /// <param name="configure">Callback to configure the AddGraphQLOptions</param>
    /// <returns></returns>
    public static IServiceCollection AddGraphQLSchema<TSchemaContext>(this IServiceCollection serviceCollection, Action<AddGraphQLOptions<TSchemaContext>> configure)
    {
        // We don't want the DI of JsonSerializerOptions as they may not be set up correctly for the dynamic types
        // They used IGraphQLRequestDeserializer/IGraphQLResponseSerializer to override the default JSON serialization
        serviceCollection.TryAddSingleton<IGraphQLRequestDeserializer>(new DefaultGraphQLRequestDeserializer());
        serviceCollection.TryAddSingleton<IGraphQLResponseSerializer>(new DefaultGraphQLResponseSerializer());
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var authService = serviceProvider.GetService<IAuthorizationService>();
        var webHostEnvironment = serviceProvider.GetService<IWebHostEnvironment>();

        var options = new AddGraphQLOptions<TSchemaContext>(authService);
        configure(options);

        // Apply environment-based defaults if not explicitly set
        var schemaOptions = options.Schema;

        // If user hasn't explicitly set IsDevelopment, detect from environment
        // We check if it's still the default value (true) and the environment is not Development
        if (webHostEnvironment != null && !webHostEnvironment.IsEnvironment("Development"))
        {
            schemaOptions.IsDevelopment = false;
        }

        var schema = new SchemaProvider<TSchemaContext>(
            schemaOptions.AuthorizationService,
            schemaOptions.FieldNamer,
            introspectionEnabled: schemaOptions.IntrospectionEnabled,
            isDevelopment: schemaOptions.IsDevelopment
        );

        // Apply allowed exceptions
        foreach (var allowedException in schemaOptions.AllowedExceptions)
        {
            if (!schema.AllowedExceptions.Contains(allowedException))
                schema.AllowedExceptions.Add(allowedException);
        }

        options.Builder.PreBuildSchemaFromContext?.Invoke(schema);
        if (options.AutoBuildSchemaFromContext)
            schema.PopulateFromContext(options.Builder);
        options.ConfigureSchema?.Invoke(schema);
        serviceCollection.AddSingleton(schema);

        return serviceCollection;
    }

    /// <summary>
    /// Registers the default IGraphQLValidator implementation to use as a service in your method fields to report a collection of errors
    /// </summary>
    /// <param name="serviceCollection"></param>
    /// <returns></returns>
    public static IServiceCollection AddGraphQLValidator(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddTransient<IGraphQLValidator, GraphQLValidator>();
        return serviceCollection;
    }
}
