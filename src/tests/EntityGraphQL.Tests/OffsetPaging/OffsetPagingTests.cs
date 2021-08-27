using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests.ConnectionPaging
{
    public class OffsetPagingTests
    {
        private static int peopleCnt;

        [Fact]
        public void TestGetsAll()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            FillData(data);

            schema.ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseOffsetPaging();
            var gql = new QueryRequest
            {
                Query = @"{
                    people {
                        items {
                            name id
                        }
                        hasNextPage
                        hasPreviousPage
                        totalCount
                    }
                }",
            };

            var result = schema.ExecuteQuery(gql, data, null, null);
            Assert.Null(result.Errors);

            dynamic people = result.Data["people"];
            Assert.Equal(data.People.Count, Enumerable.Count(people.items));
            Assert.Equal(data.People.Count, people.totalCount);
            Assert.False(people.hasNextPage);
            Assert.False(people.hasPreviousPage);
        }

        [Fact]
        public void TestTake()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            FillData(data);

            schema.ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseOffsetPaging();
            var gql = new QueryRequest
            {
                Query = @"{
                    people(take: 1) {
                        items {
                            name id
                        }
                        hasNextPage
                        hasPreviousPage
                        totalCount
                    }
                }",
            };

            var result = schema.ExecuteQuery(gql, data, null, null);
            Assert.Null(result.Errors);

            dynamic people = result.Data["people"];
            Assert.Equal(1, Enumerable.Count(people.edges));
            Assert.Equal(data.People.Count, people.totalCount);
            Assert.True(people.hasNextPage);
            Assert.False(people.hasPreviousPage);
        }
        [Fact]
        public void TestTakeSkip()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            FillData(data);

            schema.ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseOffsetPaging();
            var gql = new QueryRequest
            {
                Query = @"{
                    people(take: 2 sjip: 2) {
                        items {
                            name id
                        }
                        hasNextPage
                        hasPreviousPage
                        totalCount
                    }
                }",
            };

            var result = schema.ExecuteQuery(gql, data, null, null);
            Assert.Null(result.Errors);

            dynamic people = result.Data["people"];
            Assert.Equal(2, Enumerable.Count(people.edges));
            Assert.Equal(data.People.Count, people.totalCount);
            Assert.True(people.hasNextPage);
            Assert.True(people.hasPreviousPage);
        }
        [Fact]
        public void TestSkip()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            FillData(data);

            schema.ReplaceField("people", ctx => ctx.People.OrderBy(p => p.Id), "Return list of people with paging metadata")
                .UseOffsetPaging();
            var gql = new QueryRequest
            {
                Query = @"{
                    people(skip: 2) {
                        items {
                            name id
                        }
                        hasNextPage
                        hasPreviousPage
                        totalCount
                    }
                }",
            };

            var result = schema.ExecuteQuery(gql, data, null, null);
            Assert.Null(result.Errors);

            dynamic people = result.Data["people"];
            Assert.Equal(2, Enumerable.Count(people.edges));
            Assert.Equal(data.People.Count, people.totalCount);
            Assert.False(people.hasNextPage);
            Assert.True(people.hasPreviousPage);
        }

        [Fact]
        public void TestMergeArguments()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var data = new TestDataContext();
            FillData(data);

            schema.ReplaceField(
                "people",
                new
                {
                    search = (string)null
                },
                (ctx, args) => ctx.People
                    .WhereWhen(p => p.Name.Contains(args.search) || p.LastName.Contains(args.search), !string.IsNullOrEmpty(args.search))
                    .OrderBy(p => p.Id),
                "Return list of people with paging metadata")
            .UseOffsetPaging();

            var gql = new QueryRequest
            {
                Query = @"{
                    people(take: 1, search: ""ill"") {
                        items {
                            name
                        }
                        hasNextPage
                        hasPreviousPage
                        totalCount
                    }
                }",
            };

            var result = schema.ExecuteQuery(gql, data, null, null);
            Assert.Null(result.Errors);

            dynamic people = result.Data["people"];
            Assert.Equal(1, Enumerable.Count(people.edges));
            Assert.Equal(2, people.totalCount); // 2 "ill" matches
            Assert.True(people.hasNextPage);
            Assert.False(people.hasPreviousPage);
        }
        private static void FillData(TestDataContext data)
        {
            data.People = new()
            {
                MakePerson("Bill", "Murray"),
                MakePerson("John", "Frank"),
                MakePerson("Cheryl", "Crow"),
                MakePerson("Jill", "Castle"),
                MakePerson("Jack", "Snider"),
            };
        }

        private static Person MakePerson(string fname, string lname)
        {
            return new Person
            {
                Id = peopleCnt++,
                Name = fname,
                LastName = lname
            };
        }
    }
}