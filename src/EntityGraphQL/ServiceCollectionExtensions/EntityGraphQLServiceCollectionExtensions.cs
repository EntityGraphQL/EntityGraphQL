using System;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.ServiceCollectionExtensions
{
    public static class EntityGraphQLServiceCollectionExtensions
    {
        /// <summary>
        /// Adds a SchemaProvider<TSchemaContext> as a singleton to the service collection. Defaults for SchemaBuilder.FromObject<TSchemaContext>() are used.
        /// </summary>
        /// <param name="serviceCollection"></param>
        /// <typeparam name="TSchemaContext">Your base schema context type</typeparam>
        /// <returns></returns>
        public static IServiceCollection AddGraphQLSchema<TSchemaContext>(this IServiceCollection serviceCollection)
        {
            var schema = SchemaBuilder.FromObject<TSchemaContext>();
            serviceCollection.AddSingleton(schema);

            return serviceCollection;
        }
        /// <summary>
        /// Adds a SchemaProvider<TSchemaContext> as a singleton to the service collection.
        /// </summary>
        /// <param name="serviceCollection"></param>
        /// <param name="autoCreateIdArguments">If true, for any collection fields whose type has an Id field it will create a singular field with the required id argument. E.g. a people field will add a person(id) field.</param>
        /// <param name="autoCreateEnumTypes">If true any Enum types found in the context object graph are added to the schema</param>
        /// <param name="fieldNamer">Function to name fields in your schema. If null it defaults to lowerCaseFields</param>
        /// <typeparam name="TSchemaContext"></typeparam>
        /// <returns></returns>
        public static IServiceCollection AddGraphQLSchema<TSchemaContext>(this IServiceCollection serviceCollection, bool autoCreateIdArguments, bool autoCreateEnumTypes, Func<string, string> fieldNamer)
        {
            serviceCollection.AddGraphQLSchema<TSchemaContext>(autoCreateIdArguments, autoCreateEnumTypes, fieldNamer, null);

            return serviceCollection;
        }
        /// <summary>
        /// Adds a SchemaProvider<TSchemaContext> as a singleton to the service collection.
        /// </summary>
        /// <param name="serviceCollection"></param>
        /// <param name="configure">Function to further configure your schema</param>
        /// <typeparam name="TSchemaContext"></typeparam>
        /// <returns></returns>
        public static IServiceCollection AddGraphQLSchema<TSchemaContext>(this IServiceCollection serviceCollection, Action<SchemaProvider<TSchemaContext>> configure)
        {
            serviceCollection.AddGraphQLSchema(true, true, null, configure);

            return serviceCollection;
        }
        /// <summary>
        /// Adds a SchemaProvider<TSchemaContext> as a singleton to the service collection.
        /// </summary>
        /// <param name="serviceCollection"></param>
        /// <param name="autoCreateIdArguments">If true, for any collection fields whose type has an Id field it will create a singular field with the required id argument. E.g. a people field will add a person(id) field.</param>
        /// <param name="CreateEnumTypes">If true any Enum types found in the context object graph are added to the schema</param>
        /// <param name="configure">Function to further configure your schema</param>
        /// <typeparam name="TSchemaContext"></typeparam>
        /// <returns></returns>
        public static IServiceCollection AddGraphQLSchema<TSchemaContext>(this IServiceCollection serviceCollection, bool autoCreateIdArguments, bool autoCreateEnumTypes, Action<SchemaProvider<TSchemaContext>> configure)
        {
            serviceCollection.AddGraphQLSchema(autoCreateIdArguments, autoCreateEnumTypes, null, configure);

            return serviceCollection;
        }
        /// <summary>
        /// Adds a SchemaProvider<TSchemaContext> as a singleton to the service collection.
        /// </summary>
        /// <param name="serviceCollection"></param>
        /// <param name="autoCreateIdArguments">If true, for any collection fields whose type has an Id field it will create a singular field with the required id argument. E.g. a people field will add a person(id) field.</param>
        /// <param name="autoCreateEnumTypes">If true any Enum types found in the context object graph are added to the schema</param>
        /// <param name="fieldNamer">Function to name fields in your schema. If null it defaults to lowerCaseFields</param>
        /// <param name="configure">Function to further configure your schema</param>
        /// <typeparam name="TSchemaContext"></typeparam>
        /// <returns></returns>
        public static IServiceCollection AddGraphQLSchema<TSchemaContext>(this IServiceCollection serviceCollection, bool autoCreateIdArguments, bool autoCreateEnumTypes, Func<string, string> fieldNamer, Action<SchemaProvider<TSchemaContext>> configure)
        {
            var schema = SchemaBuilder.FromObject<TSchemaContext>(autoCreateIdArguments, autoCreateEnumTypes, fieldNamer);
            configure?.Invoke(schema);
            serviceCollection.AddSingleton(schema);

            return serviceCollection;
        }
    }
}