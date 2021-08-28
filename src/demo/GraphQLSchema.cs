using System.IO;
using System.Linq;
using demo.Mutations;
using EntityGraphQL.Schema;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.FieldExtensions;

namespace demo
{
    public class GraphQLSchema
    {
        public static SchemaProvider<DemoContext> MakeSchema()
        {
            // build our schema directly from the DB Context
            var demoSchema = SchemaBuilder.FromObject<DemoContext>();

            // Add custom root fields
            demoSchema.UpdateQueryType(queryType =>
            {
                demoSchema.AddType<Connection<Person>>("PersonConnection", "Metadata about a person connection (paging over people)").AddAllFields();
                queryType.AddField("writers", db => db.People.Where(p => p.WriterOf.Any()), "List of writers");
                // queryType.AddField("directors", db => db.People.Where(p => p.DirectorOf.Any()), "List of directors")
                //     .UseOrderBy();
                queryType.AddField(
                    "directors",
                    new PersonSortArgs(),
                    (ctx, args) => ctx.People.Where(p => p.DirectorOf.Any()),
                    "List of directors");

                queryType.ReplaceField("actors",
                    (db) => db.Actors.Select(a => a.Person).OrderBy(a => a.Id),
                    "actors paged by connection & edges and orderable")
                    // .UseOrderBy()
                    .UseConnectionPaging();

                queryType.AddField("actorsOffset",
                    (db) => db.Actors.Select(a => a.Person)
                            .OrderBy(a => a.Id),
                    "Actors with offset paging")
                    .UseOffsetPaging();
            });

            // Add calculated fields to a type
            demoSchema.UpdateType<Person>(type =>
            {
                type.AddField("name", l => $"{l.FirstName} {l.LastName}", "Person's name");
                // really poor example of using services e.g. you could just do below but pretend the service does something crazy like calls an API
                // type.AddField("age", l => (int)((DateTime.Now - l.Dob).TotalDays / 365), "Show the person's age");
                // AgeService needs to be added to the ServiceProvider
                type.AddField("age", person => ArgumentHelper.WithService((AgeService ageService) => ageService.Calc(person)), "Show the person's age");
                type.AddField("filteredDirectorOf", new
                {
                    filter = ArgumentHelper.EntityQuery<Movie>()
                },
                (person, args) => person.DirectorOf.WhereWhen(args.filter, args.filter.HasValue).OrderBy(a => a.Name),
                "Get Director of based on filter");
                type.ReplaceField("writerOf", m => m.WriterOf.Select(a => a.Movie), "Movies they wrote");
                type.ReplaceField("actorIn", m => m.ActorIn.Select(a => a.Movie), "Movies they acted in");
            });

            // replace fields. e.g. remove a many-to-many relationship
            demoSchema.UpdateType<Movie>(type =>
            {
                type.ReplaceField("actors", m => m.Actors.Select(a => a.Person), "Actors in the movie");
                type.ReplaceField("writers", m => m.Writers.Select(a => a.Person), "Writers in the movie");
            });

            // add some mutations (always last, or after the types they require have been added)
            demoSchema.AddInputType<Detail>("Detail", "Detail item").AddAllFields();
            demoSchema.AddMutationFrom(new DemoMutations());
            File.WriteAllText("schema.graphql", demoSchema.GetGraphQLSchema());
            return demoSchema;
        }
    }
}
