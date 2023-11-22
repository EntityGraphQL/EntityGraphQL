using Xunit;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Compiler;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.Tests
{
    public class VariableTests
    {
        [Fact]
        public void QueryWithVariable()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Query().ReplaceField("people", new { limit = ArgumentHelper.Required<int>() }, (db, p) => db.People.Take(p.limit), "List of people with limit");
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"
                    query MyQuery($limit: Int) {
                        people(limit: $limit) { id name }
                    }
                ",
                Variables = new QueryVariables
                {
                    {"limit", 5}
                }
            };
            var tree = new GraphQLCompiler(schema).Compile(gql);

            Assert.Single(tree.Operations.First().QueryFields);
            TestDataContext context = new TestDataContext().FillWithTestData();
            for (int i = 0; i < 20; i++)
            {
                context.People.Add(new Person());
            }
            var qr = tree.ExecuteQuery(context, null, gql.Variables);
            dynamic people = (dynamic)qr.Data["people"];
            // we only have the fields requested
            Assert.Equal(5, Enumerable.Count(people));
        }

        [Fact]
        public void QueryWithDefaultArguments()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Query().ReplaceField("people", new { limit = ArgumentHelper.Required<int>() }, (db, p) => db.People.Take(p.limit), "List of people with limit");
            // Add a argument field with a require parameter
            var tree = new GraphQLCompiler(schema).Compile(@"
        query MyQuery($limit: Int = 10) {
            people(limit: $limit) { id name }
        }
        ");

            Assert.Single(tree.Operations.First().QueryFields);
            TestDataContext context = new TestDataContext().FillWithTestData();
            for (int i = 0; i < 20; i++)
            {
                context.People.Add(new Person());
            }
            var qr = tree.ExecuteQuery(context, null, null);
            dynamic people = (dynamic)qr.Data["people"];
            // we only have the fields requested
            Assert.Equal(10, Enumerable.Count(people));
        }

        [Fact]
        public void QueryWithDefaultArgumentsOverrideCodeDefault()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            // code default of 5
            schema.Query().ReplaceField("people", new { limit = 5 }, (db, p) => db.People.Take(p.limit), "List of people with limit");

            // should use gql default of 6
            var tree = new GraphQLCompiler(schema).Compile(@"
        query MyQuery($limit: Int = 6) {
            people(limit: $limit) { id name }
        }
        ");

            Assert.Single(tree.Operations.First().QueryFields);
            TestDataContext context = new TestDataContext().FillWithTestData();
            for (int i = 0; i < 20; i++)
            {
                context.People.Add(new Person());
            }
            var qr = tree.ExecuteQuery(context, null, null);
            dynamic people = (dynamic)qr.Data["people"];
            // we only have the fields requested
            Assert.Equal(6, Enumerable.Count(people));
        }

        [Fact]
        public void QueryVariableDefinitionRequiredBySchemaItIsNotRequired()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            var gql = new QueryRequest
            {
                Query = @"mutation Mute($id: ID!) { # required here but not in the actual schema
                    nullableGuidArgs(id: $id)
                }",
                Variables = new QueryVariables { { "id", null } },
            };

            var serviceCollection = new ServiceCollection();
            var service = new AgeService();
            serviceCollection.AddSingleton(service);

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, serviceCollection.BuildServiceProvider(), null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field 'nullableGuidArgs' - Supplied variable 'id' is null while the variable definition is non-null. Please update query document or supply a non-null value.", results.Errors[0].Message);
        }

        [Fact]
        public void QueryVariableArrayGetsAList()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Query().AddField("test", new { ids = (Guid[])null }, (db, args) => db.People.Where(p => args.ids.Any(a => a == p.Guid)), "test field");
            var gql = new QueryRequest
            {
                Query = @"query ($ids: [ID]) {
                    test(ids: $ids) { id }
                }",
                // assume JSON deserialiser created a List<> but we need an array []
                Variables = new QueryVariables { { "ids", new[] { "03d539f8-6bbc-4b62-8f7f-b55c7eb242e6" } } },
            };

            var testSchema = new TestDataContext();
            var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
        }

        [Fact]
        public void QueryVariableArrayGetsAListRequired()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Query().AddField("test", new { ids = ArgumentHelper.Required<Guid[]>() }, (db, args) => db.People.Where(p => ((Guid[])args.ids).Any(a => a == p.Guid)), "test field");
            var gql = new QueryRequest
            {
                Query = @"query ($ids: [ID]) {
                    test(ids: $ids) { id }
                }",
                // assume JSON deserialiser created a List<> but we need an array []
                Variables = new QueryVariables { { "ids", new[] { "03d539f8-6bbc-4b62-8f7f-b55c7eb242e6" } } },
            };

            var testSchema = new TestDataContext();
            var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
        }

        // TODO - better error message
        // [Fact]
        // public void TestVariableUndefined()
        // {

        // }

        private static void MakePersonIdGuid(SchemaProvider<TestDataContext> schema)
        {
            schema.Query().ReplaceField("person",
                            new
                            {
                                id = ArgumentHelper.Required<Guid>()
                            },
                            (ctx, args) => ctx.People.FirstOrDefault(p => p.Guid == args.id),
                            "Get person by ID"
                        );
            schema.Type<Person>().ReplaceField("id", Person => Person.Guid, "ID");
        }
    }
}