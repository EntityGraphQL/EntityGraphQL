using System.Linq;
using EntityGraphQL.Schema;
using Xunit;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.Tests.IQueryableTests
{
    public class ListToSingleTests
    {
        [Fact]
        public void TestListToSingle()
        {
            var schema = SchemaBuilder.FromObject<TestDbContext>();

            schema.UpdateType<Movie>(schemaType =>
            {
                schemaType.AddField("mainActor", movie => movie.Actors.FirstOrDefault(), null);
            });

            var gql = new QueryRequest
            {
                Query = @"{
                    movies {
                        mainActor { name }
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
                new Movie { Id = 10, Name = "A New Hope", Actors = new List<Actor> { new Actor { Id = 1, Name = "Alec Guinness" }, new Actor { Id = 2, Name = "Mark Hamill" } } });
            data.SaveChanges();
            var result = schema.ExecuteRequest(gql, serviceProvider, null);
            Assert.Null(result.Errors);

            dynamic movies = result.Data["movies"];
            Assert.Equal(1, movies.Count);
            Assert.Equal("Alec Guinness", movies[0].mainActor.name);
        }
    }
}