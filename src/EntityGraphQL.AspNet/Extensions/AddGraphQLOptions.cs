using System;
using EntityGraphQL.Schema;

namespace EntityGraphQL.AspNet
{
    public class AddGraphQLOptions<TSchemaContext>
    {
        /// <summary>
        /// If true the schema builder will automatically create a singular field for any collection fields whose type has an Id field. 
        /// E.g. a people field will add a person(id) field.
        /// </summary>
        public bool AutoCreateIdArguments { get; set; } = true;
        /// <summary>
        /// If true the schema builder will automatically create any Enum types found in the context object graph.
        /// </summary>
        public bool AutoCreateEnumTypes { get; set; } = true;
        /// <summary>
        /// If true the schema will be built via reflection on the context type.
        /// If false the schema will be created with the TSchemaContext as it's context but will be empty of fields/types. 
        /// You can fully populate it in the ConfigureSchema callback
        /// </summary>
        public bool AutoBuildSchemaFromContext { get; set; } = true;
        /// <summary>
        /// Overwrite the default field naming convention. (Default is lowerCaseFields)
        /// </summary>
        public Func<string, string> FieldNamer { get; set; } = SchemaBuilder.DefaultNamer;
        /// <summary>
        /// Called after the schema object is created but before the context is reflected into it. Use for set up of type mappings or 
        /// anything that may be needed for the schema to be built correctly.
        /// </summary>
        public Action<SchemaProvider<TSchemaContext>>? PreBuildSchemaFromContext { get; set; } = null;
        /// <summary>
        /// Called after the context has been reflected into a schema to allow further customisation.
        /// Or use this to configure the whole schema if AutoBuildSchemaFromContext is false.
        /// </summary>
        public Action<SchemaProvider<TSchemaContext>>? ConfigureSchema { get; set; } = null;
    }
}