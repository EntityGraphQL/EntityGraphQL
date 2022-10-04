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
        public static void ConfigureSchema(SchemaProvider<DemoContext> demoSchema)
        {
            // Add custom root fields
            demoSchema.UpdateQuery(queryType =>
            {
                demoSchema.AddType<Connection<Person>>("PersonConnection", "Metadata about a person connection (paging over people)").AddAllFields();
                queryType.AddField("writers", db => db.People.Where(p => p.WriterOf.Any()), "List of writers");
                queryType.AddField("directors", db => db.People.Where(p => p.DirectorOf.Any()), "List of directors")
                    .UseSort();

                queryType.ReplaceField("actors",
                    (db) => db.Actors.Select(a => a.Person),
                    "actors paged by connection & edges and orderable")
                    .UseFilter()
                    .UseSort()
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
                type.AddField("age", "Show the person's age")
                    .ResolveWithService<AgeService>((person, ageService) => ageService.Calc(person));
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
            demoSchema.Mutation().AddFrom<DemoMutations>();

            File.WriteAllText("schema.graphql", demoSchema.ToGraphQLSchemaString());
        }
    }
}
