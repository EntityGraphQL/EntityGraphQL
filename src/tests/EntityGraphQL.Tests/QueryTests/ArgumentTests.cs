using Xunit;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Extensions;
using EntityGraphQL.Compiler;
using System;
using System.Collections.Generic;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// Tests the extended (non-GraphQL - came first) LINQ style querying functionality
    /// </summary>
    public class ArgumentTests
    {
        [Fact]
        public void CanExecuteRequiredParameter()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>()).Compile(@"
        {
        	project(id: 55) {
        		name
        	}
        }");

            Assert.Single(tree.Operations.First().QueryFields);
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
            Assert.Equal("Project 3", ((dynamic)result.Data["project"]).name);
        }

        [Fact]
        public void SupportsManyArguments()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderOptions { AutoCreateFieldWithIdArguments = false });
            // Add a argument field with a require parameter
            schema.Query().AddField("user", new { id = ArgumentHelper.Required<int>(), something = true }, (ctx, param) => ctx.Users.Where(u => u.Id == param.id).FirstOrDefault(), "Return a user by ID");
            var tree = new GraphQLCompiler(schema).Compile(@"query {
        	user(id: 100, something: false) { id }
        }");
            // db => db.Users.Where(u => u.Id == id).Select(u => new {id = u.Id}]).FirstOrDefault()
            dynamic result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null).Data["user"];
            // we only have the fields requested
            Assert.Equal(1, result.GetType().GetFields().Length);
            Assert.NotNull(result.GetType().GetField("id"));
            Assert.Equal(100, result.id);

            Assert.Contains("user(id: Int!, something: Boolean! = true): User", schema.ToGraphQLSchemaString());
        }

        private class UserInputObject
        {
            public string Name { get; set; }
        }

        [Fact]
        public void SupportsComplexArgumentWithDefault()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderOptions { AutoCreateFieldWithIdArguments = false, AutoCreateNewComplexTypes = true });
            schema.AddInputType<UserInputObject>("UserInput", "");
            // Add a argument field with a require parameter
            schema.Query().AddField("user", new { user = new UserInputObject() { Name = "Steve" } }, (ctx, param) => ctx.Users.FirstOrDefault(), "Return a user by ID");

            Assert.Contains("user(user: UserInput = { name: \"Steve\" }): User", schema.ToGraphQLSchemaString());
        }

        [Fact]
        public void ThrowsOnMissingRequiredArgument()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderOptions { AutoCreateFieldWithIdArguments = false });
            // Add a argument field with a require parameter
            schema.Query().AddField("user", new { id = ArgumentHelper.Required<int>() }, (ctx, param) => ctx.Users.FirstOrDefault(u => u.Id == param.id), "Return a user by ID");
            var gql = new QueryRequest
            {
                Query = @"query {
                    user { id }
                }"
            };
            var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Single(result.Errors);
            Assert.Equal("Field 'user' - missing required argument 'id'", result.Errors[0].Message);
        }

        [Fact]
        public void ThrowsOnMissingRequiredArguments()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderOptions { AutoCreateFieldWithIdArguments = false });
            // Add a argument field with a require parameter
            schema.Query().AddField("user", new { id = ArgumentHelper.Required<int>(), h = ArgumentHelper.Required<string>() }, (ctx, param) => ctx.Users.FirstOrDefault(u => u.Id == param.id), "Return a user by ID");
            var gql = new QueryRequest
            {
                Query = @"query {
                        user { id }
                    }"
            };
            var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
            Assert.Equal("Field 'user' - missing required argument 'id'", result.Errors[0].Message);
        }

        [Fact]
        public void SupportsArgumentsDefaultValue()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            // Add a argument field with a default parameter
            schema.Query().AddField("me", new { id = 100 }, (ctx, param) => ctx.Users.Where(u => u.Id == param.id).FirstOrDefault(), "Return me, or someone else");
            var tree = new GraphQLCompiler(schema).Compile(@"query {
                        me { id }
                    }");

            dynamic result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null).Data["me"];
            // we only have the fields requested
            Assert.Equal(1, result.GetType().GetFields().Length);
            Assert.NotNull(result.GetType().GetField("id"));
            Assert.Equal(100, result.id);

            Assert.Contains("me(id: Int! = 100): User", schema.ToGraphQLSchemaString());
        }

        [Fact]
        public void SupportsDefaultArgumentsInNonRoot()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddEnum("HeightUnit", typeof(HeightUnit), "Unit of height measurement");
            schema.Type<Person>().ReplaceField("height", new { unit = HeightUnit.Cm }, (p, param) => p.GetHeight(param.unit), "Return me, or someone else");
            var result = new GraphQLCompiler(schema).Compile(@"query {
                        people { id height }
                    }").ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);

            Assert.Single(result.Data);
            var person = Enumerable.First((dynamic)result.Data["people"]);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.NotNull(person.GetType().GetField("id"));
            Assert.NotNull(person.GetType().GetField("height"));
            Assert.Equal(183.0, person.height);

            Assert.Contains("height(unit: HeightUnit! = Cm): Float!", schema.ToGraphQLSchemaString());
        }

        [Fact]
        public void SupportsArgumentsInNonRootAndEnum()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddEnum("HeightUnit", typeof(HeightUnit), "Unit of height measurement");
            schema.Type<Person>().ReplaceField("height", new { unit = HeightUnit.Cm }, (p, param) => p.GetHeight(param.unit), "Return me, or someone else");
            var tree = new GraphQLCompiler(schema).Compile(@"query {
                        people { height(unit: Meter) }
                    }").ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);

            dynamic result = tree.Data["people"];
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.First(result);
            // we only have the fields requested
            Assert.Equal(1.83, person.height);
        }
        [Fact]
        public void SupportsArgumentsInNonRootAndEnumAsVar()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddEnum("HeightUnit", typeof(HeightUnit), "Unit of height measurement");
            schema.Type<Person>().ReplaceField("height",
                new { unit = HeightUnit.Cm },
                (p, param) => p.GetHeight(param.unit),
                "Return me, or someone else");

            var gql = new QueryRequest
            {
                Query = @"query People($unitType: HeightUnit) {
                    people { height(unit: $unitType) }
                }",
                Variables = new QueryVariables
                {
                    {"unitType", "Meter"}
                }
            };
            var tree = new GraphQLCompiler(schema).Compile(gql).ExecuteQuery(new TestDataContext().FillWithTestData(), null, gql.Variables, null);

            dynamic result = tree.Data["people"];
            Assert.Equal(1, Enumerable.Count(result));
            var person = Enumerable.First(result);
            // we only have the fields requested
            Assert.Equal(1.83, person.height);
        }

        [Fact]
        public void SupportsArgumentsGuid()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            MakePersonIdGuid(schema);
            // Add a argument field with a require parameter
            var tree = new GraphQLCompiler(schema).Compile(@"query {
                        person(id: ""cccccccc-bbbb-4444-1111-ccddeeff0033"") { id projects { id name } }
                    }").ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);

            dynamic user = tree.Data["person"];
            // we only have the fields requested
            Assert.Equal(2, user.GetType().GetFields().Length);
            Assert.NotNull(user.GetType().GetField("id"));
            Assert.Equal(new Guid("cccccccc-bbbb-4444-1111-ccddeeff0033"), user.id);
        }
        [Fact]
        public void SupportsArgumentsGuidAsVar()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            MakePersonIdGuid(schema);
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"query Guid($id: ID!) {
                        person(id: $id) { id projects { id name } }
                    }",
                Variables = new QueryVariables {
                    {"id", "cccccccc-bbbb-4444-1111-ccddeeff0033"}
                }
            };
            var tree = new GraphQLCompiler(schema).Compile(gql).ExecuteQuery(new TestDataContext().FillWithTestData(), null, gql.Variables);

            dynamic user = tree.Data["person"];
            // we only have the fields requested
            Assert.Equal(2, user.GetType().GetFields().Length);
            Assert.NotNull(user.GetType().GetField("id"));
            Assert.Equal(new Guid("cccccccc-bbbb-4444-1111-ccddeeff0033"), user.id);
        }

        [Fact]
        public void SupportsArgumentsInGraph()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            MakePersonIdGuid(schema);
            schema.Type<Person>().AddField("project", new { pid = ArgumentHelper.Required<int>() }, (p, args) => p.Projects.FirstOrDefault(s => s.Id == args.pid), "Return a specific project");
            // Add a argument field with a require parameter
            var tree = new GraphQLCompiler(schema).Compile(@"query {
                        person(id: ""cccccccc-bbbb-4444-1111-ccddeeff0033"") { id project(pid: 55) { id name } }
                    }").ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);

            dynamic user = tree.Data["person"];
            // we only have the fields requested
            Assert.Equal(2, user.GetType().GetFields().Length);
            Assert.NotNull(user.GetType().GetField("id"));
            Assert.Equal(new Guid("cccccccc-bbbb-4444-1111-ccddeeff0033"), user.id);
            Assert.Equal(55, user.project.id);
        }

        [Fact]
        public void QueryWithUnknownArgument()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            // Add a argument field with a require parameter
            var e = Assert.Throws<EntityGraphQLCompilerException>(() =>
            {
                var tree = new GraphQLCompiler(schema).Compile(@"
            query MyQuery($limit: Int = 10) {
                people(limit: $limit) { id name projects { id name } }
            }
            ");
            });
            Assert.Equal("No argument 'limit' found on field 'people'", e.Message);
        }

        [Fact]
        public void FloatArg()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Query().ReplaceField("users", new
            {
                f = (float?)null,
            },
            (db, p) => db.Users, "Testing float");

            var gql = new GraphQLCompiler(schema).Compile(@"
        query {
            users(f: 4.3) { id }
        }");
            var context = new TestDataContext().FillWithTestData();
            var qr = gql.ExecuteQuery(context, null, null);
            dynamic users = (dynamic)qr.Data["users"];
            // we only have the fields requested
            Assert.Equal(1, Enumerable.Count(users));
        }
        [Fact]
        public void StringArg()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Query().ReplaceField("users", new
            {
                str = (string)null,
            },
            (db, p) => db.Users.WhereWhen(u => u.Field2.Contains(p.str), !string.IsNullOrEmpty(p.str)), "Testing string");

            var gql = new GraphQLCompiler(schema).Compile(@"
        query {
            users(str: ""3"") { id }
        }");
            var context = new TestDataContext().FillWithTestData();
            var qr = gql.ExecuteQuery(context, null, null);
            dynamic users = (dynamic)qr.Data["users"];
            // we only have the fields requested
            Assert.Equal(0, Enumerable.Count(users));
        }
        [Fact]
        public void ListArg()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Query().ReplaceField("people", new
            {
                names = (List<string>)null
            },
            (db, p) => db.People.WhereWhen(per => p.names.Any(a => a == per.Name), p.names != null), "Testing list");

            var gql = new GraphQLCompiler(schema).Compile(@"
        query {
            people(names: [""bill"", ""jill""]) { name }
        }");
            var context = new TestDataContext().FillWithTestData();
            context.People.Add(new Person
            {
                Id = 99,
                Guid = new Guid("cccc34cc-abbb-4444-1111-ccddeeff0033"),
                Name = "jill",
                LastName = "Last Name",
                Birthday = new DateTime(2000, 1, 1, 1, 1, 1, 1),
                Height = 177,
            });
            var qr = gql.ExecuteQuery(context, null, null);
            dynamic people = (dynamic)qr.Data["people"];
            // we only have the fields requested
            Assert.Equal(2, context.People.Count);
            Assert.Equal(1, Enumerable.Count(people));
        }
        [Fact]
        public void ArrayArg()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Query().ReplaceField("people", new
            {
                names = (string[])null
            },
            (db, p) => db.People.WhereWhen(per => p.names.Any(a => a == per.Name), p.names != null), "Testing list");

            var gql = new GraphQLCompiler(schema).Compile(@"
        query {
            people(names: [""bill"", ""jill""]) { name }
        }");
            var context = new TestDataContext().FillWithTestData();
            context.People.Add(new Person
            {
                Id = 99,
                Guid = new Guid("cccc34cc-abbb-4444-1111-ccddeeff0033"),
                Name = "jill",
                LastName = "Last Name",
                Birthday = new DateTime(2000, 1, 1, 1, 1, 1, 1),
                Height = 177,
            });
            var qr = gql.ExecuteQuery(context, null, null);
            dynamic people = (dynamic)qr.Data["people"];
            // we only have the fields requested
            Assert.Equal(2, context.People.Count);
            Assert.Equal(1, Enumerable.Count(people));
        }
        [Fact]
        public void EnumerableArg()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Query().ReplaceField("people", new
            {
                // EntityGraphQL will automatically use a List<string>
                names = (IEnumerable<string>)null
            },
            (db, p) => db.People.WhereWhen(per => p.names.Any(a => a == per.Name), p.names != null), "Testing list");

            var gql = new GraphQLCompiler(schema).Compile(@"
        query {
            people(names: [""bill"", ""jill""]) { name }
        }");
            var context = new TestDataContext().FillWithTestData();
            context.People.Add(new Person
            {
                Id = 99,
                Guid = new Guid("cccc34cc-abbb-4444-1111-ccddeeff0033"),
                Name = "jill",
                LastName = "Last Name",
                Birthday = new DateTime(2000, 1, 1, 1, 1, 1, 1),
                Height = 177,
            });
            var qr = gql.ExecuteQuery(context, null, null);
            dynamic people = (dynamic)qr.Data["people"];
            // we only have the fields requested
            Assert.Equal(2, context.People.Count);
            Assert.Equal(1, Enumerable.Count(people));
        }
        [Fact]
        public void ObjectArg()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddInputType<PersonArg>("PersonArg", "PersonArgs").AddAllFields();
            schema.Query().ReplaceField("people", new
            {
                options = (PersonArg)null
            },
            (db, p) => db.People.WhereWhen(per => per.Name == p.options.name, p.options != null), "Testing list");

            var gql = new GraphQLCompiler(schema).Compile(@"
        query {
            people(options: {name: ""jill""}) { name }
        }");
            var context = new TestDataContext().FillWithTestData();
            context.People.Add(new Person
            {
                Id = 99,
                Guid = new Guid("cccc34cc-abbb-4444-1111-ccddeeff0033"),
                Name = "jill",
                LastName = "Last Name",
                Birthday = new DateTime(2000, 1, 1, 1, 1, 1, 1),
                Height = 177,
            });
            var qr = gql.ExecuteQuery(context, null, null);
            dynamic people = (dynamic)qr.Data["people"];
            // we only have the fields requested
            Assert.Equal(2, context.People.Count);
            Assert.Equal(1, Enumerable.Count(people));
        }
        [Fact]
        public void TestSameNameArgsOnDifferentFields()
        {
            var schema = SchemaBuilder.Create<TestDataContext>();
            schema.AddType<Person>("Person info").AddAllFields();
            schema.Query().AddField("people", db => db.People, "List of people");
            var gql = new GraphQLCompiler(schema).Compile(@"
                query {
                    people {
                        task(id: 1) { id name }
                        project(id: 2) { id name }
                    }
                }");
            var context = new TestDataContext();
            context.People.Add(new Person
            {
                Id = 99,
                Name = "jill",
                Projects = new List<Project>
                {
                    new Project
                    {
                        Id = 1,
                        Name = "Project 1"
                    },
                    new Project
                    {
                        Id = 2,
                        Name = "Project 2"
                    }
                },
                Tasks = new List<Task>
                {
                    new Task
                    {
                        Id = 1,
                        Name = "Task 1"
                    },
                    new Task
                    {
                        Id = 2,
                        Name = "Task 2"
                    }
                }
            });
            var qr = gql.ExecuteQuery(context, null, null);
            Assert.Null(qr.Errors);
            dynamic person = Enumerable.First((dynamic)qr.Data["people"]);
            // make sure we get task 1 and project 2 - i.e. the args were passed to the correct field
            Assert.Equal("Task 1", person.task.name);
            Assert.Equal(1, person.task.id);
            Assert.Equal("Project 2", person.project.name);
            Assert.Equal(2, person.project.id);
        }

        [Fact]
        public void TestSameNameArgsOnDifferentFieldsRoot()
        {
            var schema = SchemaBuilder.Create<TestDataContext>();
            schema.AddType<Task>("Task info").AddAllFields();
            schema.Query().AddField("task", new { id = ArgumentHelper.Required<int>() }, (db, args) => db.Tasks.FirstOrDefault(t => t.Id == args.id), "Get task");
            schema.Query().AddField("project", new { id = ArgumentHelper.Required<int>() }, (db, args) => db.Projects.FirstOrDefault(t => t.Id == args.id), "Get project");
            var gql = new GraphQLCompiler(schema).Compile(@"
                query {
                    task(id: 1) { id name }
                    project(id: 2) { id name }
                }");
            var context = new TestDataContext
            {
                Projects = new List<Project>
                {
                    new Project
                    {
                        Id = 1,
                        Name = "Project 1"
                    },
                    new Project
                    {
                        Id = 2,
                        Name = "Project 2"
                    }
                },
                Tasks = new List<Task>
                {
                    new Task
                    {
                        Id = 1,
                        Name = "Task 1"
                    },
                    new Task
                    {
                        Id = 2,
                        Name = "Task 2"
                    }
                }
            };
            var qr = gql.ExecuteQuery(context, null, null);
            Assert.Null(qr.Errors);
            dynamic task = qr.Data["task"];
            dynamic project = qr.Data["project"];
            // make sure we get task 1 and project 2 - i.e. the args were passed to the correct field
            Assert.Equal("Task 1", task.name);
            Assert.Equal(1, task.id);
            Assert.Equal("Project 2", project.name);
            Assert.Equal(2, project.id);
        }

        [Fact]
        public void TestSameNameArgsOnSameFieldWithAlias()
        {
            var schema = SchemaBuilder.Create<TestDataContext>();
            schema.AddType<Person>("Person info").AddAllFields();
            schema.Query().AddField("people", db => db.People, "List of people");
            var gql = new GraphQLCompiler(schema).Compile(@"
                query {
                    people {
                        task(id: 1) { id name }
                        task2: task(id: 2) { id name }
                    }
                }");
            var context = new TestDataContext();
            context.People.Add(new Person
            {
                Id = 99,
                Name = "jill",
                Tasks = new List<Task>
                {
                    new Task
                    {
                        Id = 1,
                        Name = "Task 1"
                    },
                    new Task
                    {
                        Id = 2,
                        Name = "Task 2"
                    }
                }
            });
            var qr = gql.ExecuteQuery(context, null, null);
            Assert.Null(qr.Errors);
            dynamic person = Enumerable.First((dynamic)qr.Data["people"]);
            Assert.Equal("Task 1", person.task.name);
            Assert.Equal(1, person.task.id);
            Assert.Equal("Task 2", person.task2.name);
            Assert.Equal(2, person.task2.id);
        }

        [Fact]
        public void TestSameNameArgsOnSameFieldWithAliasRoot()
        {
            var schema = SchemaBuilder.Create<TestDataContext>();
            schema.AddType<Task>("Task info").AddAllFields();
            schema.Query().AddField("task", new { id = ArgumentHelper.Required<int>() }, (db, args) => db.Tasks.FirstOrDefault(t => t.Id == args.id), "Get task");
            schema.Query().AddField("project", new { id = ArgumentHelper.Required<int>() }, (db, args) => db.Projects.FirstOrDefault(t => t.Id == args.id), "Get project");
            var gql = new GraphQLCompiler(schema).Compile(@"
                query {
                    task(id: 1) { id name }
                    task2: task(id: 2) { id name }
                }");
            var context = new TestDataContext
            {
                Tasks = new List<Task>
                {
                    new Task
                    {
                        Id = 1,
                        Name = "Task 1"
                    },
                    new Task
                    {
                        Id = 2,
                        Name = "Task 2"
                    }
                }
            };
            var qr = gql.ExecuteQuery(context, null, null);
            Assert.Null(qr.Errors);
            dynamic task = qr.Data["task"];
            dynamic task2 = qr.Data["task2"];
            Assert.Equal("Task 1", task.name);
            Assert.Equal(1, task.id);
            Assert.Equal("Task 2", task2.name);
            Assert.Equal(2, task2.id);
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
    public class PersonArg
    {
        public string name { get; set; }
    }
}