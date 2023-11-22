using Xunit;
using EntityGraphQL.Schema;
using System.Collections.Generic;
using EntityGraphQL.Directives;
using System.Linq.Expressions;
using System;
using EntityGraphQL.Compiler;
using System.Linq;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// Tests directives
    /// </summary>
    public class DirectiveTests
    {
        [Fact]
        public void TestIncludeIfTrueConstant()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var query = new QueryRequest
            {
                Query = @"query {
                    people {
                        id
                        name @include(if: true)
                    }
                }"
            };
            var result = schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
            dynamic person = ((dynamic)result.Data["people"])[0];
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "name");
        }
        [Fact]
        public void TestIncludeIfFalseConstant()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var query = new QueryRequest
            {
                Query = @"query {
                    people {
                        id
                        name @include(if: false)
                    }
                }"
            };
            var result = schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
            Assert.Null(result.Errors);
            dynamic person = ((dynamic)result.Data["people"])[0];
            Assert.Single(person.GetType().GetFields());
            Assert.NotNull(person.GetType().GetField("id"));
        }

        [Fact]
        public void TestIncludeIfFalseConstantOnIdField()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var query = new QueryRequest
            {
                Query = @"query {
                    person(id:99) @include(if: false) {
                        id
                        name 
                    }
                }"
            };
            var result = schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
            Assert.Null(result.Errors);
            Assert.False(result.Data.ContainsKey("person"));
        }

        [Fact]
        public void TestIncludeIfFalseConstantOnList()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var query = new QueryRequest
            {
                Query = @"query {
                    people @include(if: false) {
                        id
                        name 
                    }
                }"
            };
            var result = schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
            Assert.Null(result.Errors);

            Assert.False(result.Data.ContainsKey("people"));
        }


        [Fact]
        public void TestIncludeIfTrueVariable()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var query = new QueryRequest
            {
                Query = @"query MyQuery($include: Boolean!){
    people {
        id
        name @include(if: $include)
    }
}",
                Variables = new QueryVariables { { "include", true } }
            };
            var result = schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
            dynamic person = ((dynamic)result.Data["people"])[0];
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "name");
        }

        [Fact]
        public void TestSkipIfTrueConstant()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var query = new QueryRequest
            {
                Query = @"query {
    people {
        id
        name @skip(if: true)
    }
}"
            };
            var result = schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
            dynamic person = ((dynamic)result.Data["people"])[0];
            Assert.Single(person.GetType().GetFields());
            Assert.NotNull(person.GetType().GetField("id"));
        }
        [Fact]
        public void TestSkipIfFalseConstant()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var query = new QueryRequest
            {
                Query = @"query {
    people {
        id
        name @skip(if: false)
    }
}"
            };
            var result = schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
            dynamic person = ((dynamic)result.Data["people"])[0];
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.Contains((IEnumerable<dynamic>)person.GetType().GetFields(), f => f.Name == "name");
        }
        [Fact]
        public void TestSkipIfFalseVariable()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var query = new QueryRequest
            {
                Query = @"query MyQuery($skip: Boolean!){
    people {
        id
        name @skip(if: $skip)
    }
}",
                Variables = new QueryVariables { { "skip", true } }
            };
            var result = schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
            dynamic person = ((dynamic)result.Data["people"])[0];
            Assert.Equal(1, person.GetType().GetFields().Length);
            Assert.NotNull(person.GetType().GetField("id"));
        }

        [Fact]
        public void TestDirectiveRoot()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var query = new QueryRequest
            {
                Query = @"query MyQuery($skip: Boolean!){
                    people @skip(if: $skip) {
                        id
                        name 
                    }
                }",
                Variables = new QueryVariables { { "skip", true } }
            };
            var result = schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
            Assert.Null(result.Errors);
            Assert.Empty(result.Data);
        }

        [Fact]
        public void TestDirectiveList()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var query = new QueryRequest
            {
                Query = @"query MyQuery($skip: Boolean!){
    people {
        id
        name 
        projects @skip(if: $skip) {
            name
        }
    }
}",
                Variables = new QueryVariables { { "skip", true } }
            };
            var result = schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
            var person = ((dynamic)result.Data["people"])[0];
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.NotNull(person.GetType().GetField("id"));
            Assert.NotNull(person.GetType().GetField("name"));
        }

        [Fact]
        public void TestDirectiveObject()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var query = new QueryRequest
            {
                Query = @"query MyQuery($skip: Boolean!){
    people {
        id
        name 
        manager @skip(if: $skip) {
            name
        }
    }
}",
                Variables = new QueryVariables { { "skip", true } }
            };
            var result = schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
            var person = ((dynamic)result.Data["people"])[0];
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.NotNull(person.GetType().GetField("id"));
            Assert.NotNull(person.GetType().GetField("name"));
        }

        [Fact]
        public void TestDirectiveOnMutation()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            var mutationCalled = false;
            schema.Mutation().Add("addPerson", (PeopleMutationsArgs args) =>
            {
                mutationCalled = true;
                return new Person { Name = string.IsNullOrEmpty(args.Name) ? "Default" : args.Name, Id = 555, Projects = new List<Project>() };
            }, new SchemaBuilderOptions { AutoCreateInputTypes = true });
            var query = new QueryRequest
            {
                Query = @"mutation MyQuery($skip: Boolean!){
                    addPerson(name: ""test"") @skip(if: $skip) {
                        id
                        name 
                    }
                }",
                Variables = new QueryVariables { { "skip", true } }
            };
            var result = schema.ExecuteRequestWithContext(query, new TestDataContext().FillWithTestData(), null, null, null);
            Assert.False(mutationCalled);
            Assert.Null(result.Errors);
            Assert.Empty(result.Data);
        }

        [Fact]
        public void TestDirectiveInSchemaOutputWithArgs()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            var result = schema.ToGraphQLSchemaString();
            Assert.Contains("directive @skip(if: Boolean!) on FIELD | FRAGMENT_SPREAD | INLINE_FRAGMENT", result);
        }

        [Fact]
        public void TestCustomDirectiveOnFieldInSchemaOutput()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddDirective(new ExampleDirective());

            var result = schema.ToGraphQLSchemaString();
            Assert.Contains("directive @example on FIELD", result);
        }

        [Fact]
        public void TestCustomDirectiveOnExpressionFormat()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddDirective(new FormatDirective());

            var query = new QueryRequest
            {
                Query = @"query {
                    people {
                        id
                        birthday @format(as: ""dd MMM yyyy"")
                    }
                }",
            };
            var data = new TestDataContext();
            data.People.Add(new Person { Id = 1, Birthday = new DateTime(2000, 1, 1) });
            var result = schema.ExecuteRequestWithContext(query, data, null, null, null);
            var person = ((dynamic)result.Data["people"])[0];
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.NotNull(person.GetType().GetField("id"));
            Assert.Equal(1, person.id);
            Assert.Equal("01 Jan 2000", person.birthday);
        }
    }

    internal class ExampleDirective : DirectiveProcessor<object>
    {
        public override string Name => "example";

        public override string Description => "Actually does nothing";

        public override List<ExecutableDirectiveLocation> Location => new() { ExecutableDirectiveLocation.FIELD };
    }

    internal class FormatDirective : DirectiveProcessor<FormatDirectiveArgs>
    {
        public override string Name => "format";
        public override string Description => "Formats DateTime scalar values";
        public override List<ExecutableDirectiveLocation> Location => new() { ExecutableDirectiveLocation.FIELD };

        public override IGraphQLNode VisitNode(ExecutableDirectiveLocation location, IGraphQLNode node, object arguments)
        {
            if (location == ExecutableDirectiveLocation.FIELD && arguments is FormatDirectiveArgs args)
            {
                if (node is GraphQLScalarField fieldNode)
                {
                    var expression = fieldNode.NextFieldContext;
                    if (expression.Type != typeof(DateTime) && expression.Type != typeof(DateTime?))
                        throw new EntityGraphQLException("The format directive can only be used on DateTime fields");

                    if (expression.Type == typeof(DateTime?))
                        expression = Expression.Property(expression, "Value");
                    expression = Expression.Call(expression, "ToString", null, Expression.Constant(args.As));
                    return new GraphQLScalarField(fieldNode.Schema, fieldNode.Field, fieldNode.Name, expression, fieldNode.RootParameter, fieldNode.ParentNode, fieldNode.Arguments);
                }
            }
            return node;
        }
    }

    internal class FormatDirectiveArgs
    {
        [GraphQLField("as", "The format to use")]
        public string As { get; set; }
    }
}