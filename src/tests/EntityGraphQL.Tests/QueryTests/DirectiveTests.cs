using Xunit;
using EntityGraphQL.Schema;
using System.Collections.Generic;
using EntityGraphQL.Directives;

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
            var result = schema.ExecuteRequest(query, new TestDataContext().FillWithTestData(), null, null, null);
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
            var result = schema.ExecuteRequest(query, new TestDataContext().FillWithTestData(), null, null, null);
            Assert.Null(result.Errors);
            dynamic person = ((dynamic)result.Data["people"])[0];
            Assert.Single(person.GetType().GetFields());
            Assert.NotNull(person.GetType().GetField("id"));
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
            var result = schema.ExecuteRequest(query, new TestDataContext().FillWithTestData(), null, null, null);
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
            var result = schema.ExecuteRequest(query, new TestDataContext().FillWithTestData(), null, null, null);
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
            var result = schema.ExecuteRequest(query, new TestDataContext().FillWithTestData(), null, null, null);
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
            var result = schema.ExecuteRequest(query, new TestDataContext().FillWithTestData(), null, null, null);
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
            var result = schema.ExecuteRequest(query, new TestDataContext().FillWithTestData(), null, null, null);
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
            var result = schema.ExecuteRequest(query, new TestDataContext().FillWithTestData(), null, null, null);
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
            var result = schema.ExecuteRequest(query, new TestDataContext().FillWithTestData(), null, null, null);
            var person = ((dynamic)result.Data["people"])[0];
            Assert.Equal(2, person.GetType().GetFields().Length);
            Assert.NotNull(person.GetType().GetField("id"));
            Assert.NotNull(person.GetType().GetField("name"));
        }

        [Fact]
        public void TestDirectiveOnMutation()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();
            schema.AddMutationsFrom<PeopleMutations>();
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
            var result = schema.ExecuteRequest(query, new TestDataContext().FillWithTestData(), null, null, null);
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
    }

    internal class ExampleDirective : DirectiveProcessor<object>
    {
        public override string Name => "example";

        public override string Description => "Actually does nothing";

        public override List<ExecutableDirectiveLocation> On => new() { ExecutableDirectiveLocation.FIELD };
    }
}