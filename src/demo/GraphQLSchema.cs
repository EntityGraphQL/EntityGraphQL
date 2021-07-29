using System;
using System.IO;
using System.Linq;
using demo.Mutations;
using EntityGraphQL.Schema;
using EntityGraphQL.Extensions;
using System.Text;
using EntityGraphQL.Schema.Connections;

namespace demo
{
    public class GraphQLSchema
    {
        public static SchemaProvider<DemoContext> MakeSchema()
        {
            // build our schema directly from the DB Context
            var demoSchema = SchemaBuilder.FromObject<DemoContext>();

            // we can extend the schema

            demoSchema.AddType<ConnectionPageInfo>("PageInfo", "Metadata about a page of data").AddAllFields();
            demoSchema.AddType<ConnectionEdge<Person>>("PersonEdge", "Metadata about an edge of page result").AddAllFields();
            demoSchema.AddType<Connection<Person>>("PersonConnection", "Metadata about a person connection (paging over people)").AddAllFields();


            // Add custom root fields
            demoSchema.UpdateQueryType(queryType =>
            {
                queryType.ReplaceField("actors", new
                {
                    filter = ArgumentHelper.EntityQuery<Person>()
                }, (db, param) => db.People.Where(p => p.ActorIn.Any()).WhereWhen(param.filter, param.filter.HasValue), "List of actors");
                queryType.AddField("writers", db => db.People.Where(p => p.WriterOf.Any()), "List of writers");
                queryType.AddField("directors", db => db.People.Where(p => p.DirectorOf.Any()), "List of directors");

                queryType.AddField("actorsConnection2",
                    new ConnectionArgs(),
                    new ResolveFactory<DemoContext, ConnectionArgs, Connection<Person>>
                    {
                        PreSelection = (db, args) => new Connection<Person>
                        {
                            Edges = db.Actors.Select(a => a.Person)
                                    .OrderBy(a => a.Id)
                                    .Skip(DeserializeCursor(args.after) ?? (!string.IsNullOrEmpty(args.before) ? DeserializeCursor(args.before).Value - 1 - args.last.Value : 0))
                                    .Take(args.first ?? Math.Min(args.last.Value, DeserializeCursor(args.before).Value - 1))
                                    .Select(a => new ConnectionEdge<Person>
                                    {
                                        Node = a,
                                        Cursor = null,
                                        Id = -1
                                    }),
                            PageInfo = args.first.HasValue ? new ConnectionPageInfo
                            {
                                EndCursor = SerializeCursor(Math.Min(args.first.Value, db.Actors.Select(a => a.Person).Count() - DeserializeCursor(args.after) ?? 0), DeserializeCursor(args.after)),
                                HasNextPage = ((DeserializeCursor(args.after) ?? 0) + args.first) < db.Actors.Select(a => a.Person).Count(),
                                HasPreviousPage = (DeserializeCursor(args.after) ?? 0) > 0,
                                StartCursor = SerializeCursor(DeserializeCursor(args.after) ?? 0, 1)
                            } : new ConnectionPageInfo
                            {
                                EndCursor = SerializeCursor(0, DeserializeCursor(args.before) - 1),
                                HasNextPage = DeserializeCursor(args.before) < db.Actors.Select(a => a.Person).Count(),
                                HasPreviousPage = (DeserializeCursor(args.before) - 1 - args.last.Value) > 0,
                                StartCursor = SerializeCursor(DeserializeCursor(args.before) ?? 0, -args.last)
                            },
                            TotalCount = db.Actors.Select(a => a.Person).Count()
                        },
                        PostSelection = (ctx, args) => new Connection<Person>
                        {
                            Edges = ctx.Edges.Select((a, idx) => new ConnectionEdge<Person>
                            {
                                Node = a.Node,
                                Cursor = SerializeCursor(idx + 1, !string.IsNullOrEmpty(args.after) ? DeserializeCursor(args.after) : DeserializeCursor(args.before) - args.last - 1),
                                Id = a.Id
                            }),
                            PageInfo = ctx.PageInfo,
                            TotalCount = ctx.TotalCount
                        }
                    },
                    "actors paged by connection & edges",
                    "PersonConnection");

                // example of GraphQL style connections & edges
                queryType.AddField("actorsConnection",
                    new
                    {
                        // forward pagination
                        first = (int?)null,
                        after = (string)null,
                        // backward pagination
                        last = (int?)null,
                        before = (string)null
                    },
                    (db, args) =>
                        // {
                        //     if (args.first.HasValue && args.last.HasValue ||
                        //         !string.IsNullOrEmpty(args.after) && !string.IsNullOrEmpty(args.before))
                        //         throw new ArgumentException($"Please only supply either forard pagination arguments (first with optional after) or backward pagination (last with optional before). Not both.");
                        //     if (!args.last.HasValue && !args.first.HasValue)
                        //         throw new ArgumentException($"Please provide at least the first or last argument");

                        new Connection<Person>
                        {
                            Edges = db.Actors.Select(a => a.Person)
                                .OrderBy(a => a.Id)
                                .Skip(DeserializeCursor(args.after) ?? (!string.IsNullOrEmpty(args.before) ? DeserializeCursor(args.before).Value - 1 - args.last.Value : 0))
                                .Take(args.first ?? Math.Min(args.last.Value, DeserializeCursor(args.before).Value - 1))
                                .ToList()
                                .Select((a, idx) => new ConnectionEdge<Person>
                                {
                                    Node = a,
                                    Cursor = SerializeCursor(idx + 1, !string.IsNullOrEmpty(args.after) ? DeserializeCursor(args.after) : DeserializeCursor(args.before) - args.last - 1),
                                    Id = idx + 1 + (!string.IsNullOrEmpty(args.after) ? DeserializeCursor(args.after).Value : 0)
                                }),
                            PageInfo = args.first.HasValue ? new ConnectionPageInfo
                            {
                                EndCursor = SerializeCursor(Math.Min(args.first.Value, db.Actors.Select(a => a.Person).Count() - DeserializeCursor(args.after) ?? 0), DeserializeCursor(args.after)),
                                HasNextPage = ((DeserializeCursor(args.after) ?? 0) + args.first) < db.Actors.Select(a => a.Person).Count(),
                                HasPreviousPage = (DeserializeCursor(args.after) ?? 0) > 0,
                                StartCursor = SerializeCursor(DeserializeCursor(args.after) ?? 0, 1)
                            } : new ConnectionPageInfo
                            {
                                EndCursor = SerializeCursor(0, DeserializeCursor(args.before) - 1),
                                HasNextPage = DeserializeCursor(args.before) < db.Actors.Select(a => a.Person).Count(),
                                HasPreviousPage = (DeserializeCursor(args.before) - 1 - args.last.Value) > 0,
                                StartCursor = SerializeCursor(DeserializeCursor(args.before) ?? 0, -args.last)
                            },
                            TotalCount = db.Actors.Select(a => a.Person).Count()
                            // }
                        },
                    "actors paged by connection & edges",
                    "PersonConnection");
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

            // demoSchema.AddField("actorsConnection",
            //     db => db.Actors.Select(a => a.Person)
            //                 .OrderBy(a => a.Id),
            //     "Get a list of actors")
            //     .MakeConnection();

            // add some mutations (always last, or after the types they require have been added)
            demoSchema.AddInputType<Detail>("Detail", "Detail item").AddAllFields();
            demoSchema.AddMutationFrom(new DemoMutations());
            File.WriteAllText("schema.graphql", demoSchema.GetGraphQLSchema());
            return demoSchema;
        }

        private static string SerializeCursor(int idx, int? from)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes((from.HasValue ? from.Value + idx : idx).ToString()));
        }

        private static int? DeserializeCursor(string after)
        {
            if (string.IsNullOrEmpty(after))
                return null;
            return int.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(after)));
        }
    }
}
