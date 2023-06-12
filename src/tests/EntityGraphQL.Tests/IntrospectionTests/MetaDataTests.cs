using Xunit;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Compiler;
using static EntityGraphQL.Tests.ServiceFieldTests;
using Microsoft.Extensions.DependencyInjection;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// Tests GraphQL metadata
    /// </summary>
    public class MetadataTests
    {
        [Fact]
        public void Supports__typename()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            // Add a argument field with a require parameter
            var tree = new GraphQLCompiler(schemaProvider).Compile(@"query {
	users { __typename id }
}");

            var users = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
            var user = Enumerable.First((dynamic)users.Data["users"]);
            // we only have the fields requested
            Assert.Equal(2, user.GetType().GetFields().Length);
            Assert.NotNull(user.GetType().GetField("__typename"));
            Assert.Equal("User", user.__typename);
        }

        [Fact]
        public void TestServiceFieldTypeWithTypename()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();
            schema.Type<Project>().AddField("settings", "Return settings")
                .ResolveWithService<ConfigService>((p, c) => c.Get(p.Id))
                .IsNullable(false);

            var gql = new QueryRequest
            {
                Query = @"query {
                    projects {
                        settings {
                            type
                            __typename
                        }
                    }
                }"
            };

            var context = new TestDataContext().FillWithTestData();

            var serviceCollection = new ServiceCollection();
            ConfigService service = new();
            serviceCollection.AddSingleton(service);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);

            Assert.Null(res.Errors);
            Assert.Equal("ProjectConfig", ((dynamic)res.Data["projects"])[0].settings.__typename);
        }

        [Fact]
        public void TestTypenameOnAllTypes()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddType<ProjectConfig>("ProjectConfig").AddAllFields();
            schema.Type<Project>().AddField("settings", "Return settings")
                .ResolveWithService<ConfigService>((p, c) => c.Get(p.Id))
                .IsNullable(false);

            var gql = new QueryRequest
            {
                Query = @"query {
                    projects {
                        # on list
                        __typename
                        settings {
                            # on service
                            __typename
                        }
                        owner {
                            # on object
                            __typename
                        }
                    }
                    project(id: 1) {
                        __typename
                    }
                }"
            };

            var context = new TestDataContext().FillWithTestData();

            var serviceCollection = new ServiceCollection();
            ConfigService service = new();
            serviceCollection.AddSingleton(service);

            var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);

            Assert.Null(res.Errors);
            Assert.Equal("ProjectConfig", ((dynamic)res.Data["projects"])[0].settings.__typename);
        }

        [Fact]
        public void TestConnectionPaging__typename()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext().FillWithTestData();

            schema.Query().ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseConnectionPaging();
            var gql = new QueryRequest
            {
                Query = @"{
                    people {
                        edges {
                            node {
                                name
                                __typename
                            }
                            __typename
                        }
                        pageInfo {
                            __typename
                        }
                        __typename
                    }
                }",
            };

            var result = schema.ExecuteRequestWithContext(gql, data, null, null);
            Assert.Null(result.Errors);

            dynamic people = result.Data["people"];
            Assert.Equal(data.People.Count, Enumerable.Count(people.edges));
            Assert.Equal("PersonConnection", people.__typename);
            Assert.Equal("Person", people.edges[0].node.__typename);
            Assert.Equal("PersonEdge", people.edges[0].__typename);
            Assert.Equal("PageInfo", people.pageInfo.__typename);
        }
    }
}