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
        schema.DocumentExecutor = serviceProvider.GetRequiredService<IGraphQLDocumentExecutor>();

        foreach (var allowedException in schemaOptions.AllowedExceptions)
        {
            if (!schema.AllowedExceptions.Contains(allowedException))
                schema.AllowedExceptions.Add(allowedException);
        }

        options.Builder.PreBuildSchemaFromContext?.Invoke(schema);
        if (options.AutoBuildSchemaFromContext)
            schema.PopulateFromContext(options.Builder);
        options.ConfigureSchema?.Invoke(schema);

        return schema;
    }

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
        serviceCollection.TryAddSingleton<IGraphQLDocumentExecutor, DefaultGraphQLDocumentExecutor>();

        var options = new AddGraphQLOptions<TSchemaContext>(null);
        configure(options);

        serviceCollection.Add(new ServiceDescriptor(typeof(SchemaProvider<TSchemaContext>), sp => BuildSchema(sp, options), options.SchemaLifetime));
        serviceCollection.Add(new ServiceDescriptor(typeof(ISchemaProvider<TSchemaContext>), sp => sp.GetRequiredService<SchemaProvider<TSchemaContext>>(), options.SchemaLifetime));

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
