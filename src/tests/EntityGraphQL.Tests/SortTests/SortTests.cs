using Xunit;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using System;

namespace EntityGraphQL.Tests
{
    public class SortTests
    {
        [Fact]
        public void SupportUseSort()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Type<TestDataContext>().GetField("people", null)
                .UseSort();
            var gql = new QueryRequest
            {
                Query = @"query($sort: [QueryPeopleSortInput]) {
                    people(sort: $sort) { lastName }
                }",
                Variables = new QueryVariables {
                    {"sort", new [] { new {lastName = SortDirection.DESC } } }
                }
            };
            var context = new TestDataContext().FillWithTestData();
            context.People.Add(new Person
            {
                LastName = "Zoo"
            });
            var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic people = ((IDictionary<string, object>)tree.Data)["people"];
            Assert.Equal(2, Enumerable.Count(people));
            var person = Enumerable.First(people);
            Assert.Equal("Zoo", person.lastName);
        }
        [Fact]
        public void TestSortAttribute()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext2>();

            var gql = new QueryRequest
            {
                Query = @"query($sort: [QueryPeopleSortInput]) {
                    people(sort: $sort) { lastName }
                }",
                Variables = new QueryVariables{
                    {"sort", new [] { new {lastName = "DESC" } } }
                }
            };
            TestDataContext2 context = new();
            context.FillWithTestData();
            context.People.Add(new Person
            {
                LastName = "Zoo"
            });
            var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic people = ((IDictionary<string, object>)tree.Data)["people"];
            Assert.Equal(2, Enumerable.Count(people));
            var person = Enumerable.First(people);
            Assert.Equal("Zoo", person.lastName);
        }
        private class TestDataContext2 : TestDataContext
        {
            [UseSort]
            public override List<Person> People { get; set; } = new List<Person>();
        }

        [Fact]
        public void SupportUseSortSelectSortFields()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Type<TestDataContext>().GetField("people", null)
                .UseSort((Person person) => new
                {
                    person.Height,
                    person.Name
                });
            var gql = new QueryRequest
            {
                Query = @"query($sort: [QueryPeopleSortInput]) {
                    people(sort: $sort) { lastName }
                }",
                Variables = new QueryVariables{
                    {"sort", new [] { new {height = "ASC" } } }
                }
            };
            var context = new TestDataContext().FillWithTestData();
            context.People.Add(new Person
            {
                LastName = "Zoo",
                Height = 1
            });
            var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic people = ((IDictionary<string, object>)tree.Data)["people"];
            Assert.Equal(2, Enumerable.Count(people));
            var person = Enumerable.First(people);
            Assert.Equal("Zoo", person.lastName);
            var schemaType = schema.Type("QueryPeopleSortInput");
            var fields = schemaType.GetFields().ToList();
            Assert.Equal(3, fields.Count);
            Assert.Equal("__typename", fields[0].Name);
            Assert.Equal("height", fields[1].Name);
            Assert.Equal("name", fields[2].Name);
        }
        [Fact]
        public void SupportUseSortDefaultWithSelectSortFields()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Type<TestDataContext>().GetField("people", null)
                .UseSort((Person person) => new
                {
                    person.Height,
                    person.Name
                },
                (Person person) => person.LastName, SortDirection.DESC);
            var gql = new QueryRequest
            {
                Query = @"query {
                    people { lastName }
                }",
            };
            var context = new TestDataContext().FillWithTestData();
            context.People.Add(new Person
            {
                LastName = "Zoo",
                Height = 1
            });
            var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic people = ((IDictionary<string, object>)tree.Data)["people"];
            Assert.Equal(2, Enumerable.Count(people));
            var person = Enumerable.First(people);
            Assert.Equal("Zoo", person.lastName);
            var schemaType = schema.Type("QueryPeopleSortInput");
            var fields = schemaType.GetFields().ToList();
            Assert.Equal(3, fields.Count);
            Assert.Equal("__typename", fields[0].Name);
            Assert.Equal("height", fields[1].Name);
            Assert.Equal("name", fields[2].Name);
        }
        [Fact]
        public void SupportUseSortDefault()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Type<TestDataContext>().GetField("people", null)
                .UseSort((Person person) => person.Height, SortDirection.ASC);
            var gql = new QueryRequest
            {
                Query = @"query {
                    people { lastName }
                }",
            };
            var context = new TestDataContext().FillWithTestData();
            context.People.Add(new Person
            {
                LastName = "Zoo",
                Height = 1
            });
            var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic people = ((IDictionary<string, object>)tree.Data)["people"];
            Assert.Equal(2, Enumerable.Count(people));
            var person = Enumerable.First(people);
            Assert.Equal("Zoo", person.lastName);
            var schemaType = schema.Type("QueryPeopleSortInput");
            var fields = schemaType.GetFields().ToList();
            Assert.Equal(13, fields.Count);
            Assert.Contains("people(sort: [QueryPeopleSortInput!] = [{ height: ASC }]): [Person!]", schema.ToGraphQLSchemaString());
        }

        [Fact]
        public void SupportUseSortDefaultMulti()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Type<TestDataContext>().GetField("people", null)
                .UseSort(
                    new Sort<Person>((person) => person.Height, SortDirection.ASC),
                    new Sort<Person>((person) => person.LastName, SortDirection.ASC)
                );
            var gql = new QueryRequest
            {
                Query = @"query {
                    people { lastName }
                }",
            };
            var context = new TestDataContext().FillWithTestData();
            context.People.Add(new Person
            {
                LastName = "Zoo",
                Height = 10
            });
            context.People.Add(new Person
            {
                LastName = "Abe",
                Height = 10
            });
            var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic people = ((IDictionary<string, object>)tree.Data)["people"];
            Assert.Equal(3, Enumerable.Count(people));
            var person = Enumerable.First(people);
            Assert.Equal("Abe", person.lastName);
            var schemaType = schema.Type("QueryPeopleSortInput");
            var fields = schemaType.GetFields().ToList();
            Assert.Equal(13, fields.Count);
            Assert.Contains("people(sort: [QueryPeopleSortInput!] = [{ height: ASC }, { lastName: ASC }]): [Person!]", schema.ToGraphQLSchemaString());
        }

        [Fact]
        public void SupportUseSortOnNonRoot()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Type<Project>().GetField("tasks", null)
                .UseSort();
            var gql = new QueryRequest
            {
                Query = @"query($sort: [ProjectTasksSortInput]) {
                    projects {
                        tasks(sort: $sort) { id }
                    }
                }",
                Variables = new QueryVariables {
                    {"sort", new [] { new {id = "DESC" } } }
                }
            };
            var context = new TestDataContext().FillWithTestData();
            var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic projects = ((IDictionary<string, object>)tree.Data)["projects"];
            Assert.Equal(1, Enumerable.Count(projects));
            var project = Enumerable.First(projects);
            Assert.Equal(4, Enumerable.Count(project.tasks));
            Assert.Equal(4, Enumerable.ElementAt(project.tasks, 0).id);
            Assert.Equal(3, Enumerable.ElementAt(project.tasks, 1).id);
            Assert.Equal(2, Enumerable.ElementAt(project.tasks, 2).id);
            Assert.Equal(1, Enumerable.ElementAt(project.tasks, 3).id);
        }
        [Fact]
        public void SupportUseSortOnNonRootVariableWithClass()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Type<Project>().GetField("tasks", null)
                .UseSort();
            var gql = new QueryRequest
            {
                Query = @"query($sort: [ProjectTasksSortInput]) {
                    projects {
                        tasks(sort: $sort) { id }
                    }
                }",
                Variables = new QueryVariables
                {
                    { "sort", new List<IdSort>{new IdSort { Id = SortDirection.DESC } }}
                }
            };
            var context = new TestDataContext().FillWithTestData();
            var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic projects = ((IDictionary<string, object>)tree.Data)["projects"];
            Assert.Equal(1, Enumerable.Count(projects));
            var project = Enumerable.First(projects);
            Assert.Equal(4, Enumerable.Count(project.tasks));
            Assert.Equal(4, Enumerable.ElementAt(project.tasks, 0).id);
            Assert.Equal(3, Enumerable.ElementAt(project.tasks, 1).id);
            Assert.Equal(2, Enumerable.ElementAt(project.tasks, 2).id);
            Assert.Equal(1, Enumerable.ElementAt(project.tasks, 3).id);
        }
        [Fact]
        public void SupportUseSortOnNonRoot2()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Type<Project>().GetField("tasks", null)
                .UseSort();
            var gql = new QueryRequest
            {
                Query = @"query($sort: [ProjectTasksSortInput]) {
                    project(id: 55) {
                        tasks(sort: $sort) { id }
                    }
                }",
                Variables = new QueryVariables{
                    { "sort", new [] { new {id = "DESC" } } }
                }
            };
            var context = new TestDataContext().FillWithTestData();
            var tree = schema.ExecuteRequestWithContext(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic project = ((IDictionary<string, object>)tree.Data)["project"];
            Assert.Equal(4, Enumerable.Count(project.tasks));
            Assert.Equal(4, Enumerable.ElementAt(project.tasks, 0).id);
            Assert.Equal(3, Enumerable.ElementAt(project.tasks, 1).id);
            Assert.Equal(2, Enumerable.ElementAt(project.tasks, 2).id);
            Assert.Equal(1, Enumerable.ElementAt(project.tasks, 3).id);
        }
    }

    internal class IdSort
    {
        public SortDirection Id { get; set; }
    }

    internal class TestArgs
    {
        public SortDirection? lastName { get; set; }
    }
}