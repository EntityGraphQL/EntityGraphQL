using EntityGraphQL.Schema;

namespace demo
{
    public class GraphQLSchema
    {
        public static MappedSchemaProvider<DemoContext> MakeSchema()
        {
            // build our schema directly from the DB Context
            var demoSchema = SchemaBuilder.FromObject<DemoContext>();

            // we can extend the schema
            demoSchema.Type<Location>().AddField("idAndName", l => l.Id + " - " + l.Name, "Show ID and Name of location");
            return demoSchema;
        }
    }
}