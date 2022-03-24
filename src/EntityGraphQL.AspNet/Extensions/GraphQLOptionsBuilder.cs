using System;
using EntityGraphQL.Schema;

namespace EntityGraphQL.AspNet
{
    public class GraphQLOptionsBuilder<TSchemaContext>
    {
        public bool AutoCreateIdArguments { get; set; } = true;
        public bool AutoCreateEnumTypes { get; set; } = true;
        public Func<string, string> FieldNamer { get; set; } = SchemaBuilder.DefaultNamer;
        /// <summary>
        /// Called after the schema object is created but before the context is reflected into it. Use for set up of type mappings
        /// </summary>
        public Action<SchemaProvider<TSchemaContext>>? PreBuildSchemaFromContext { get; set; } = null;
        /// <summary>
        /// Called after the context has been reflected into a schema to allow further customisation
        /// </summary>
        public Action<SchemaProvider<TSchemaContext>>? ConfigureSchema { get; set; } = null;
    }
}