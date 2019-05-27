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

            var dto = demoSchema.AddType<PersonPagination>(nameof(PersonPagination), "Actor Pagination", null);
            dto.AddField("total", x => x.Total, "total records to match search");
            dto.AddField("pageCount", x => x.PageCount, "total pages based on page size");
            dto.AddField("people", x => x.People, "collection of people");

            demoSchema.AddField("ActorPager",
                new { page = 1, pagesize = 10, search = "" },
                (db, p) => PaginateActors(db, p),
                "Pagination. [defaults: page = 1, pagesize = 10]",
                "ActorPagination");

            // add some mutations (always last, or after the types they require have been added)
            demoSchema.AddMutationFrom(new DemoMutations());
            return demoSchema;
        }

        public static PersonPagination PaginateActors(DemoContext db, dynamic arg)
        {
            int page = (int)arg.page;
            int pagesize = (int)arg.pagesize;
            string search = (string)arg.search;

            //Filters with defaults (could use library like; LinqKit)
            System.Linq.Expressions.Expression<Func<Person, bool>> predicate = x => !x.IsDeleted;

            if (!string.IsNullOrEmpty(search))
                predicate = x => !x.IsDeleted && (x.FirstName.Contains(search) || x.LastName.Contains(search));

            //Pagination
            int total = db.People.Where(predicate).Count();
            int pagecount = ((total + pagesize) / pagesize);
            int skipTo = (page * pagesize) - (pagesize);

            //Data
            var people = db.People
                .Where(predicate)
                .OrderBy(x => x.LastName)
                .Skip(skipTo)
                .Take(pagesize);

            return new PersonPagination { Total = total, PageCount = pagecount, People = people };
        }
    }

    public class PersonPagination : Pagination
    {
        public IQueryable<Person> People { get; set; }
    }

    public class Pagination
    {
        public int Total { get; set; }

        public int PageCount { get; set; }
    }
}