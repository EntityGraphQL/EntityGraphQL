using System;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EntityGraphQL.AspNet
{
    public static class EntityGraphQLAspNetServiceCollectionExtensions
    {
        /// <summary>
        /// Adds a SchemaProvider<TSchemaContext> as a singleton to the service collection.
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
        /// Adds a SchemaProvider<TSchemaContext> as a singleton to the service collection.
        /// </summary>
        /// <typeparam name="TSchemaContext">Context type to build the schema on</typeparam>
        /// <param name="serviceCollection"></param>
        /// <param name="configure">Callback to configure the AddGraphQLOptions</param>
        /// <returns></returns>
        public static IServiceCollection AddGraphQLSchema<TSchemaContext>(this IServiceCollection serviceCollection, Action<AddGraphQLOptions<TSchemaContext>> configure)
        {
            serviceCollection.TryAddSingleton<IGraphQLRequestDeserializer>(new DefaultGraphQLRequestDeserializer());
            serviceCollection.TryAddSingleton<IGraphQLResponseSerializer>(new DefaultGraphQLResponseSerializer());
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var authService = serviceProvider.GetService<IAuthorizationService>();
            var webHostEnvironment = serviceProvider.GetService<IWebHostEnvironment>();

            var options = new AddGraphQLOptions<TSchemaContext>();
            configure(options);

            var schema = new SchemaProvider<TSchemaContext>(
                new PolicyOrRoleBasedAuthorization(authService), options.FieldNamer,
                isDevelopment: webHostEnvironment?.IsEnvironment("Development") ?? true
                );
            options.PreBuildSchemaFromContext?.Invoke(schema);
            if (options.AutoBuildSchemaFromContext)
                schema.PopulateFromContext(options);
            options.ConfigureSchema?.Invoke(schema);
            serviceCollection.AddSingleton(schema);

            return serviceCollection;
        }
        /// <summary>
        /// Registers the default IGraphQLValidator implementation to use as a service in your method fields to report a colletion of errors
        /// </summary>
        /// <param name="serviceCollection"></param>
        /// <returns></returns>
        public static IServiceCollection AddGraphQLValidator(this IServiceCollection serviceCollection)
        {
            serviceCollection.TryAddScoped<IGraphQLValidator, GraphQLValidator>();
            return serviceCollection;
        }
    }
}