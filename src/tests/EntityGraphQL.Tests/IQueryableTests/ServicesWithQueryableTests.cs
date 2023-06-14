using System.Linq;
using EntityGraphQL.Schema;
using Xunit;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using static EntityGraphQL.Tests.ServiceFieldTests;
using System;

namespace EntityGraphQL.Tests.IQueryableTests
{
    public class ServicesWithQueryableTests
    {

        [Fact]
        public void TestServiceFieldWithQueryable()
        {
            var schema = SchemaBuilder.FromObject<TestDbContext>();

            schema.AddType<ProjectConfig>("Config").AddAllFields();

            schema.Type<Movie>().AddField("config", "Get configs if they exists")
                .ResolveWithService<ConfigService>((p, srv) => srv.Get(p.Id))
                .IsNullable(false);
            schema.Type<Movie>().AddField("mainActor", p => p.Actors.FirstOrDefault(), "Actor");

            var gql = new QueryRequest
            {
                Query = @"{
                     movies {
                         config { type }
                         mainActor { name }
                     }
                 }"
            };

            var serviceCollection = new ServiceCollection();
            var srv = new ConfigService();
            serviceCollection.AddSingleton(srv);
            serviceCollection.AddDbContext<TestDbContext>(opt => opt.UseInMemoryDatabase("TestServiceFieldWithQueryable"));
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var data = serviceProvider.GetRequiredService<TestDbContext>();
            data.Movies.AddRange(
                new Movie { Id = 10, Name = "A New Hope", Actors = new List<Actor> { new Actor { Id = 1, Name = "Alec Guinness" }, new Actor { Id = 2, Name = "Mark Hamill" } } });
            data.SaveChanges();

            var res = schema.ExecuteRequest(gql, serviceProvider, null);
            Assert.Null(res.Errors);
            var movies = (dynamic)res.Data["movies"];
            Type movieType = Enumerable.First(movies).GetType();
            Assert.Equal(2, movieType.GetFields().Length);
            Assert.Equal("config", movieType.GetFields()[0].Name);
            // null check should not cause multiple calls
            Assert.Equal(1, srv.CallCount);
        }
    }
}