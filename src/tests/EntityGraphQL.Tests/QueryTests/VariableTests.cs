using Xunit;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Compiler;
using System;

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
            var tree = new GraphQLCompiler(schema).Compile(new QueryRequest
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
            });

            Assert.Single(tree.Operations.First().QueryFields);
            TestDataContext context = new TestDataContext().FillWithTestData();
            for (int i = 0; i < 20; i++)
            {
                context.People.Add(new Person());
            }
            var qr = tree.ExecuteQuery(context, null);
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
            var qr = tree.ExecuteQuery(context, null);
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
            var qr = tree.ExecuteQuery(context, null);
            dynamic people = (dynamic)qr.Data["people"];
            // we only have the fields requested
            Assert.Equal(6, Enumerable.Count(people));
        }
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