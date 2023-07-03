using System;
using System.Collections.Generic;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Schema
{

    public delegate void OnFieldCreated(IField field);
    /// <summary>
    /// Options used by SchemaBuilder when reflection the object graph to auto create schema types & fields
    /// </summary>
    public class SchemaBuilderOptions
    {
        /// <summary>
        /// List properties or field names to ignore. Default includes a list of EF properties
        /// </summary>
        public HashSet<string> IgnoreProps { get; set; } = new()
        {
            "Database",
            "Model",
            "ChangeTracker",
            "ContextId"
        };
        /// <summary>
        /// List of dotnet Types to ignore when adding types to the schema
        /// </summary>
        public HashSet<Type> IgnoreTypes { get; set; } = new()
        {
        };
        /// <summary>
        /// If true when SchemaBuilder encounters a field that returns a list of entities and the entity has a property 
        /// or field named Id SchemaBuilder will also create a schema field with a singular name and an argument of id for that entity.
        /// e.g. if it sees IEnumerable<Person> People; It will create the schema fields
        /// {
        ///   people: [Person]
        ///   person(id: ID!): Person
        /// }
        /// </summary>
        public bool AutoCreateFieldWithIdArguments { get; set; } = true;
        /// <summary>
        /// If true (default) and an enum type is encountered during reflection of the object graph it will be added to the schema 
        /// as an Enum
        /// </summary>
        public bool AutoCreateEnumTypes { get; set; } = true;
        /// <summary>
        /// If true (default) and an object type is encountered during reflection of the query object graph it will be added to the schema 
        /// as a Type including it's fields. If that type is an interface it will be added as an interface. This includes return 
        /// types form mutations
        /// </summary>
        public bool AutoCreateNewComplexTypes { get; set; } = true;
        /// <summary>
        /// If true (default = false), any object type that is encountered during reflection of the object graph that has abstract
        /// or interface types (regardless of if they are referenced by other fields), those will be added to the schema as an 
        /// Interface including it's fields
        /// </summary>
        public bool AutoCreateInterfaceTypes { get; set; }
        /// <summary>
        /// If true (default = true) and an object type is encountered during reflection of method parameters it will be added to the schema as an InputObject type if used as an argument.
        /// 
        /// If you set it false, you will need to add any InputTypes to the schema before add the methods fields/mutation/subscriptions.
        /// </summary>
        public bool AutoCreateInputTypes { get; set; } = true;
        /// <summary>
        /// If true (default = false) Any public method in the mutation/subscription class will be added to the schema as a mutation/subsscription. 
        /// If false only methodds with GraphQLMutationAttribute/GraphQLSubscriptionAttribute will be added.
        /// </summary>
        public bool AddNonAttributedMethodsInControllers { get; set; }

        public OnFieldCreated? OnFieldCreated { get; set; }
    }

    /// <summary>
    /// Options used by the SchemaBuilder factory with creating the SchemaProvider
    /// </summary>
    public class SchemaBuilderSchemaOptions
    {
        public static readonly Func<string, string> DefaultFieldNamer = name =>
        {
            return name[..1].ToLowerInvariant() + name[1..];
        };
        /// <summary>
        /// Function to name fields when reading the properties from reflection. Default is camelCase
        /// </summary>
        public Func<string, string> FieldNamer { get; set; } = DefaultFieldNamer;
        /// <summary>
        /// If true (default) schema introspection will be enabled for the schema
        /// </summary>
        public bool IntrospectionEnabled { get; set; } = true;
        /// <summary>
        /// 
        /// </summary>
        public IGqlAuthorizationService AuthorizationService { get; set; } = new RoleBasedAuthorization();
        /// <summary>
        /// Called after the schema object is created but before the context is reflected into it. Use for set up of type mappings or 
        /// anything that may be needed for the schema to be built correctly.
        /// </summary>
        public Action<ISchemaProvider>? PreBuildSchemaFromContext { get; set; }

        /// <summary>
        /// If true (default), exceptions will have their messages rendered in the 'errors' object
        /// If false, exceptions not in AllowedExceptions will have their message replaced with 'Error occurred'
        /// </summary>
        public bool IsDevelopment { get; set; } = true;

        /// <summary>
        /// List of allowed exceptions that will be rendered in the 'errors' object when IsDevelopment is false
        /// </summary>
        public List<AllowedException> AllowedExceptions { get; set; } = new List<AllowedException> {
            new AllowedException(typeof(EntityGraphQLArgumentException)),
            new AllowedException(typeof(EntityGraphQLException)),
            new AllowedException(typeof(EntityGraphQLFieldException)),
            new AllowedException(typeof(EntityGraphQLAccessException)),
            new AllowedException(typeof(EntityGraphQLValidationException)),
        };
    }
}
