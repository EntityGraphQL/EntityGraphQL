using Xunit;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using EntityGraphQL.Schema;
using System.Linq.Expressions;
using System;
using static EntityGraphQL.Tests.ComplexFieldsTests;
using static EntityGraphQL.Schema.ArgumentHelper;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// Tests graphql metadata
    /// </summary>
    public class ErrorTests
    {
        [Fact]
        public void MutationReportsError()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($name: String) {
  addPersonError(name: $name)
}
",
                Variables = new QueryVariables {
                    {"name", "Bill"}
                }
            };

            var testSchema = new TestSchema();
            var results = schemaProvider.ExecuteQuery(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field error: addPersonError - Name can not be null (Parameter 'name')", results.Errors[0].Message);
        }

        [Fact]
        public void QueryReportsError()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>(false);
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"{
    people { error }
}",
            };

            var testSchema = new TestSchema();
            var results = schemaProvider.ExecuteQuery(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field error: people - Field failed to execute", results.Errors[0].Message);
        }
    }
}