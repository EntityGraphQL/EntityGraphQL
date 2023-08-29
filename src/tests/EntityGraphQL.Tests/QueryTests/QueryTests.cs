using Xunit;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Compiler;
using System.Collections.Generic;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// Tests the extended (non-GraphQL - came first) LINQ style querying functionality
    /// </summary>
    public class QueryTests
    {
        [Fact]
        public void CanParseSimpleQuery()
        {
            var objectSchemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            var tree = new GraphQLCompiler(objectSchemaProvider).Compile(@"
{
	people { id name }
}");
            Assert.Single(tree.Operations);
            Assert.Single(tree.Operations.First().QueryFields);
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
            Assert.Single(result.Data);
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "id");
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "name");
        }

        [Fact]
        public void CanQueryAsyncField()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            var result = schemaProvider.ExecuteRequestWithContext(new QueryRequest
            {
                Query = @"{
                    firstUserId
                }"
            }, new TestDataContext().FillWithTestData(), null, null);
            Assert.Equal(100, (int?)result.Data["firstUserId"]);
        }

        [Fact]
        public void CanQueryExtendedFields()
        {
            var objectSchemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            objectSchemaProvider.Type<Person>().AddField("thing", p => p.Id + " - " + p.Name, "A weird field I want");
            var tree = new GraphQLCompiler(objectSchemaProvider).Compile(@"
{
	people { id thing }
}");
            Assert.Single(tree.Operations);
            Assert.Single(tree.Operations.First().QueryFields);
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
            Assert.Single(result.Data);
            object person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "id");
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "thing");
        }

        [Fact]
        public void CanRemoveFields()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.Type<Person>().RemoveField(p => p.Id);
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => { var tree = new GraphQLCompiler(schema).Compile(@"
            {
                people { id }
            }"); });
            Assert.Equal("Field 'id' not found on type 'Person'", ex.Message);
        }

        [Fact]
        public void WildcardQueriesHonorRemovedFieldsOnObject()
        {
            // empty schema
            var schema = SchemaBuilder.Create<TestDataContext>();
            schema.AddType<Person>("Person").AddField("name", p => p.Name, "Person's name");
            schema.Query().AddField("person", new { id = ArgumentHelper.Required<int>() }, (p, args) => p.People.FirstOrDefault(p => p.Id == args.id), "Person");
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => new GraphQLCompiler(schema).Compile(@"
            {
                person(id: 1)
            }"));
            Assert.Equal("Field 'person' requires a selection set defining the fields you would like to select.", ex.Message);
        }

        [Fact]
        public void CanParseMultipleEntityQuery()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>()).Compile(@"
            {
                people { id name }
                users { id }
            }");

            Assert.Single(tree.Operations);
            Assert.Equal(2, tree.Operations.First().QueryFields.Count);
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
            Assert.Equal(1, Enumerable.Count((dynamic)result.Data["people"]));
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "id");
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "name");

            Assert.Equal(1, Enumerable.Count((dynamic)result.Data["users"]));
            var user = Enumerable.ElementAt((dynamic)result.Data["users"], 0);
            // we only have the fields requested
            Assert.Single(user.GetType().GetFields());
            Assert.NotNull(user.GetType().GetField("id"));
        }

        [Fact]
        public void CanParseQueryWithRelation()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>()).Compile(@"
{
	people { id name user { field1 } }
}");
            // People.Select(p => new { Id = p.Id, Name = p.Name, User = new { Field1 = p.User.Field1 })
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
            Assert.Equal(1, Enumerable.Count((dynamic)result.Data["people"]));
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(3, person.GetType().GetFields().Length);
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "id");
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "name");
            // make sure we sub-select correctly to make the requested object graph
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "user");
            var user = person.user;
            Assert.Single(user.GetType().GetFields());
            Assert.NotNull(user.GetType().GetField("field1"));
        }

        [Fact]
        public void CanParseQueryWithRelationDeep()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>()).Compile(@"
        {
        	people {
                id name
        		user {
        			field1
        			nestedRelation { id name }
        		}
        	}
        }");
            // People.Select(p => new { Id = p.Id, Name = p.Name, User = new { Field1 = p.User.Field1, NestedRelation = new { Id = p.User.NestedRelation.Id, Name = p.User.NestedRelation.Name } })
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
            Assert.Equal(1, Enumerable.Count((dynamic)result.Data["people"]));
            var person = Enumerable.ElementAt((dynamic)result.Data["people"], 0);
            // we only have the fields requested
            Assert.Equal(3, person.GetType().GetFields().Length);
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "id");
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "name");
            // make sure we sub-select correctly to make the requested object graph
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "user");
            var user = person.user;
            Assert.Equal(2, user.GetType().GetFields().Length);
            Assert.Contains((IEnumerable<dynamic>)user.GetType().GetFields(), f => f.Name == "field1");
            Assert.Contains((IEnumerable<dynamic>)user.GetType().GetFields(), f => f.Name == "nestedRelation");
            var nested = person.user.nestedRelation;
            Assert.Equal(2, nested.GetType().GetFields().Length);
            Assert.Contains((IEnumerable<dynamic>)nested.GetType().GetFields(), f => f.Name == "id");
            Assert.Contains((IEnumerable<dynamic>)nested.GetType().GetFields(), f => f.Name == "name");
        }

        [Fact]
        public void FailsNonExistingField()
        {
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>()).Compile(@"
        {
        	people { id
        		projects {
        			name
        			blahs { id name }
        		}
        	}
        }"));
            Assert.Equal("Field 'blahs' not found on type 'Project'", ex.Message);
        }
        [Fact]
        public void FailsNonExistingField2()
        {
            var ex = Assert.Throws<EntityGraphQLCompilerException>(() => new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>()).Compile(@"
        {
        	people { id
        		projects {
        			name3
        		}
        	}
        }"));
            Assert.Equal("Field 'name3' not found on type 'Project'", ex.Message);
        }

        [Fact]
        public void TestAlias()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>()).Compile(@"
        {
        	projects {
        		n: name
        	}
        }");

            Assert.Single(tree.Operations.First().QueryFields);
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
            Assert.Equal("Project 3", ((dynamic)result.Data["projects"])[0].n);
        }

        [Fact]
        public void TestAliasDeep()
        {
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestDataContext>()).Compile(@"
        {
        people { id
        		projects {
        			n: name
        		}
        	}
        }");

            Assert.Single(tree.Operations.First().QueryFields);
            var result = tree.ExecuteQuery(new TestDataContext().FillWithTestData(), null, null);
            Assert.Equal("Project 3", Enumerable.First(Enumerable.First((dynamic)result.Data["people"]).projects).n);
        }

        [Fact]
        public void EnumTest()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            var gql = new QueryRequest
            {
                Query = @"{
  people {
      gender
  }
}
",
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
        }

        [Fact]
        public void DateScalarsTest()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            var gql = new QueryRequest
            {
                Query = @"{
  projects {
      created
      updated
  }
}
",
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var result = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(result.Errors);
            Assert.NotNull(((dynamic)result.Data["projects"])[0].created);
            Assert.NotNull(((dynamic)result.Data["projects"])[0].updated);
        }

        [Fact]
        public void TestTopLevelScalar()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            var gql = new GraphQLCompiler(schemaProvider).Compile(@"
query {
    totalPeople
}");

            var context = new TestDataContext();
            context.People.Clear();
            for (int i = 0; i < 15; i++)
            {
                context.People.Add(new Person());
            }
            var qr = gql.ExecuteQuery(context, null, null);
            dynamic totalPeople = (dynamic)qr.Data["totalPeople"];
            // we only have the fields requested
            Assert.Equal(15, totalPeople);
        }

        [Fact]
        public void TestDeepQuery()
        {
            var schemaProvider = SchemaBuilder.FromObject<DeepContext>();
            var gql = new QueryRequest
            {
                Query = @"query deep { levelOnes { levelTwo { level3 { name }} }}",
            };

            var testSchema = new DeepContext();

            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(results.Errors);
        }

        [Fact]
        public void TestNoArgumentsOnEnum()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            var ex = Assert.Throws<EntityQuerySchemaException>(() => schema.Type<Gender>().AddField("invalid", new { id = (int?)null }, (ctx, args) => 8, "Invalid field"));
            Assert.Equal("Field invalid on type Gender has arguments but is a GraphQL Enum type and can not have arguments.", ex.Message);
        }
        [Fact]
        public void TestNoFieldsOnScalar()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            var ex = Assert.Throws<EntityQuerySchemaException>(() => schema.Type<string>().AddField("invalid", (ctx) => 8, "Invalid field"));
            Assert.Equal("Cannot add field invalid to type String, as String is a scalar type and can not have fields.", ex.Message);
        }


        /// <summary>
        /// from issue https://github.com/EntityGraphQL/EntityGraphQL/issues/229
        /// </summary>
        [Fact]
        public void TestResolveWithServiceAndNavigationProp()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.UpdateType<Project>(x =>
            {
                x.AddField("sum", "").Resolve(y => y.Tasks.Count());
                x.AddField("test", "").ResolveWithService<TestDataContext>((y, db) => y.Updated.Value.AddDays(3));
            });

            var testSchema = new TestDataContext()
            {
                Projects = new List<Project>()
                {
                    new Project()
                    {
                        Updated = new System.DateTime(2001, 1, 1),
                        Tasks = new  List<Task>()
                        {
                            new Task(),new Task(),new Task(),
                        }
                    }
                }
            };

            var gql = new QueryRequest
            {
                Query = @"query deep { projects { sum }}",
            };

            var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Equal(3, ((dynamic)results.Data)["projects"][0].sum);

            gql = new QueryRequest
            {
                Query = @"query deep { projects { test }}",
            };


            results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Equal(new System.DateTime(2001, 1, 4), ((dynamic)results.Data)["projects"][0].test);

            gql = new QueryRequest
            {
                Query = @"query deep { projects {
                    test 
                    sum
                }}",
            };


            results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);

            Assert.Equal(3, ((dynamic)results.Data)["projects"][0].sum);
            Assert.Equal(new System.DateTime(2001, 1, 4), ((dynamic)results.Data)["projects"][0].test);
        }


        [Fact]
        public void TestResolveWithServiceEvaluatesOnce()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            int v = 0;
            var func = () => { return v++; };
            schema.Query().AddField("test", "").ResolveWithService<TestDataContext>((y, db) => func());

            var testSchema = new TestDataContext()
            {
                Projects = new List<Project>()
                {
                    new Project()
                    {

                    }
                }
            };

            var gql = new QueryRequest
            {
                Query = @"query deep { test }",
            };

            var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Equal(1, v);

        }

        [Fact]
        public void TestResolveWithServiceEvaluatesOnceForEnumerables()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            int v = 0;
            var func = () => { v++; return new List<int>(); };
            schema.Query().AddField("test", "").ResolveWithService<TestDataContext>((y, db) => func());

            var testSchema = new TestDataContext()
            {
                Projects = new List<Project>()
                {
                    new Project()
                    {

                    }
                }
            };

            var gql = new QueryRequest
            {
                Query = @"query deep { test }",
            };

            var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Equal(1, v);
        }


        public class UserDbContextNullable
        {
            public List<string> UserIds { get; set; }
        }

        /// <summary>
        /// from issue https://github.com/EntityGraphQL/EntityGraphQL/issues/221
        /// </summary>
        [Fact]
        public void TestForExtractingDataFromICollectionListEtcReturnsNull_221()
        {
            var schema = SchemaBuilder.FromObject<UserDbContextNullable>();

            var testSchema = new UserDbContextNullable() { };

            var gql = new QueryRequest
            {
                Query = @"query deep { userIds }",
            };

            var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.Null(((dynamic)results.Data)["userIds"]);
        }

        public class UserDbContextNonNullable
        {
            [GraphQLNotNull]
            public List<string> UserIds { get; set; }
        }

        /// <summary>
        /// from issue https://github.com/EntityGraphQL/EntityGraphQL/issues/221
        /// </summary>
        [Fact]
        public void TestForExtractingDataFromICollectionListEtcReturnsEmptyList_221()
        {
            var schema = SchemaBuilder.FromObject<UserDbContextNonNullable>();

            var testSchema = new UserDbContextNonNullable() { };

            var gql = new QueryRequest
            {
                Query = @"query deep { userIds }",
            };

            var results = schema.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(((dynamic)results.Data)["userIds"]);
            Assert.Empty(((dynamic)results.Data)["userIds"]);
        }
    }

    public class DeepContext
    {
        public IList<LevelOne> LevelOnes { get; set; } = new List<LevelOne>();

        public class LevelOne
        {
            public LevelTwo LevelTwo { get; set; }
        }

        public class LevelTwo
        {
            public ICollection<LevelThree> Level3 { get; set; } = new List<LevelThree>();
        }

        public class LevelThree
        {
            public string Name { get; set; }
        }
    }
}