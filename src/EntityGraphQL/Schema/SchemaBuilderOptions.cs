using System;
using System.Collections.Generic;

namespace EntityGraphQL.Schema
{
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
        /// List of dotnet type names (.FullName - including namespace) to ignore when adding types to the schema
        /// </summary>
        public HashSet<string> IgnoreTypes { get; set; } = new()
        {
        };
        /// <summary>
        /// If true when SchemaBuilder encounters a field that returns a list of entities and the entity has a property 
        /// or field name Id t will also create a schema field with a singular name and an argument of id for that entity.
        /// e.g. if it sees IEnumerable<Person> People; It will create the schema fields
        /// {
        ///   people: [Person]
        ///   person(id: ID!): Person
        /// }
        /// </summary>
        public bool AutoCreateFieldWithIdArguments { get; set; } = true;
        /// <summary>
        /// If true (default) and an enum type is encountered during reflection of the object graph it will be added to the schema as an Enum
        /// </summary>
        public bool AutoCreateEnumTypes { get; set; } = true;
        /// <summary>
        /// If true (default) and an object type is encountered during reflection of the object graph it will be added to the schema as a Type including it's fields. If that type is an interface it will be added as an interface
        /// </summary>
        public bool AutoCreateNewComplexTypes { get; set; } = true;
        /// <summary>
        /// If true (default = false), any object type that is encountered during reflection of the object graph that has abstract or interface types (regardless of if they are referenced by other fields), those will be added to the schema as an Interface including it's fields
        /// </summary>
        public bool AutoCreateInterfaceTypes { get; set; } = false;

    }

    public class SchemaBuilderMutationOptions : SchemaBuilderOptions
    {
        public bool AutoCreateInputTypes { get; set; } = true;
        public bool AddNonAttributedMethods { get; set; } = false;
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
    }
}
