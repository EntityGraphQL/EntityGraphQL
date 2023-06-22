using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;
using System.Collections.Generic;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace EntityGraphQL.Tests.IQueryableTests
{
    public class FieldExtensionsOnQueryableTests
    {

        [Fact]
        public void TestServicesInOffsetPaging()
        {
            var schema = SchemaBuilder.FromObject<TestDbContext>();
            schema.UpdateQuery(queryType =>
            {
                queryType.AddField("moviesByOffset", ctx => ctx.Movies, null)
                    .UseOffsetPaging();
            });

            schema.UpdateType<Movie>(schemaType =>
            {
                schemaType.ReplaceField("actors", null)
                    .ResolveWithService<ActorService>((m, actors) => actors.GetByMovie(m.Id));
                schemaType.AddField("mainActor", null)
                    .ResolveWithService<ActorService>((m, actors) => actors.GetFirstActorNameByMovie(m.Id));
            });

            var gql = new QueryRequest
            {
                Query = @"{
                    moviesByOffset {
                        items {
                            mainActor
                            actors { name }
                        }
                    }
                }",
            };

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ActorService>();

            using var factory = new TestDbContextFactory();
            var data = factory.CreateContext();
            serviceCollection.AddSingleton(data);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            data.Movies.AddRange(
                new Movie { Id = 1, Name = "A New Hope" },
                new Movie { Id = 2, Name = "The Empire Strike Back" },
                new Movie { Id = 3, Name = "Return of the Jedi" });
            data.SaveChanges();
            var result = schema.ExecuteRequest(gql, serviceProvider, null);
            Assert.Null(result.Errors);

            dynamic movies = result.Data["moviesByOffset"];
            Assert.Equal(3, movies.items.Count);
            Assert.Equal("Alec Guinness", movies.items[0].mainActor);
            Assert.Equal(2, movies.items[0].actors.Count);
            Assert.Equal("Alec Guinness", movies.items[0].actors[0].name);
            Assert.Equal("Mark Hamill", movies.items[0].actors[1].name);
        }
    }

    internal class ActorService
    {
        private static readonly IDictionary<int, IEnumerable<Actor>> peopleByMovies = new Dictionary<int, IEnumerable<Actor>>()
        {
            [1] = new Actor[] {
            new() { Id = 1, Name = "Alec Guinness" },
            new() { Id = 2, Name = "Mark Hamill" },
        },
            [2] = new Actor[] {
            new() { Id = 1, Name = "Carrie Fisher" },
            new() { Id = 2, Name = "Mark Hamill" },
        },
            [3] = new Actor[] {
            new() { Id = 1, Name = "Harrison Ford" },
            new() { Id = 2, Name = "Mark Hamill" },
        }
        };

        public IEnumerable<Actor> GetByMovie(int movieId)
        {
            return peopleByMovies.ContainsKey(movieId) ? peopleByMovies[movieId] : Array.Empty<Actor>();
        }

        public string GetFirstActorNameByMovie(int movieId)
        {
            return peopleByMovies.ContainsKey(movieId) ? peopleByMovies[movieId].FirstOrDefault()?.Name : null;
        }
    }
}