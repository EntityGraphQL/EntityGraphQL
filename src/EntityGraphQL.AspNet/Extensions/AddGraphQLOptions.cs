using System;
using EntityGraphQL.Schema;

namespace EntityGraphQL.AspNet
{
    public class AddGraphQLOptions<TSchemaContext> : SchemaBuilderOptions
    {
        /// <summary>
        /// If true the schema will be built via reflection on the context type. You can customise this with the properties inherited from SchemaBuilderOptions
        /// If false the schema will be created with the TSchemaContext as it's context but will be empty of fields/types. 
        /// You can fully populate it in the ConfigureSchema callback
        /// </summary>
        public bool AutoBuildSchemaFromContext { get; set; } = true;
        /// <summary>
        /// Overwrite the default field naming convention. (Default is lowerCaseFields)
        /// </summary>
        public Func<string, string> FieldNamer { get; set; } = SchemaBuilderSchemaOptions.DefaultFieldNamer;
        /// <summary>
        /// Called after the schema object is created but before the context is reflected into it. Use for set up of type mappings or 
        /// anything that may be needed for the schema to be built correctly.
        /// </summary>
        public Action<SchemaProvider<TSchemaContext>>? PreBuildSchemaFromContext { get; set; }
        /// <summary>
        /// Called after the context has been reflected into a schema to allow further customisation.
        /// Or use this to configure the whole schema if AutoBuildSchemaFromContext is false.
        /// </summary>
        public Action<SchemaProvider<TSchemaContext>>? ConfigureSchema { get; set; }
    }
}