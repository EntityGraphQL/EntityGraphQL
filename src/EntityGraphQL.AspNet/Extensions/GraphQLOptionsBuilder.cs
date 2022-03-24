using System;
using EntityGraphQL.Schema;

namespace EntityGraphQL.AspNet
{
    public class GraphQLOptionsBuilder<TSchemaContext>
    {
        public bool AutoCreateIdArguments { get; set; } = true;
        public bool AutoCreateEnumTypes { get; set; } = true;
        public Func<string, string> FieldNamer { get; set; } = SchemaBuilder.DefaultNamer;
        public Action<SchemaProvider<TSchemaContext>>? ConfigureSchema { get; set; } = null;
    }
}