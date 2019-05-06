using System;
using System.Collections.Generic;
using System.Linq;
using demo.Mutations;
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

            // Add custom root fields
            demoSchema.ReplaceField("actors", db => db.People.Where(p => p.ActorIn.Any()), "List of actors");
            demoSchema.AddField("writers", db => db.People.Where(p => p.WriterOf.Any()), "List of writers");
            demoSchema.AddField("directors", db => db.People.Where(p => p.DirectorOf.Any()), "List of directors");

            // Add calculated fields to a type
            demoSchema.Type<Person>().AddField("age", l => (int)((DateTime.Now - l.Dob).TotalDays / 365), "Show the person's age");
            demoSchema.Type<Person>().AddField("name", l => $"{l.FirstName} {l.LastName}", "Person's name");

            // replace fields. e.g. remove a many-to-many relationships
            demoSchema.Type<Movie>().ReplaceField("actors", m => m.Actors.Select(a => a.Person), "Actors in the movie");
            demoSchema.Type<Movie>().ReplaceField("writers", m => m.Writers.Select(a => a.Person), "Writers in the movie");

            demoSchema.Type<Person>().ReplaceField("writerOf", m => m.WriterOf.Select(a => a.Movie), "Movies they wrote");
            demoSchema.Type<Person>().ReplaceField("actorIn", m => m.ActorIn.Select(a => a.Movie), "Movies they acted in");

            // add some mutations (always last, or after the types they require have been added)
            demoSchema.AddMutationFrom(new DemoMutations());
            return demoSchema;
        }
    }
}