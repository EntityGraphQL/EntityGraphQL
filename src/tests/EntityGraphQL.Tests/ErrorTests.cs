using System;
using Xunit;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Tests
{
    public class ErrorTests
    {
        [Fact]
        public void MutationReportsError()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
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

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            // error from execution that prevented a valid response, the data entry in the response should be null
            Assert.Null(results.Data);
            Assert.Equal("Field 'addPersonError' - Name can not be null (Parameter 'name')", results.Errors[0].Message);
        }

        [Fact]
        public void QueryReportsError()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"{
    people { error }
}",
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field 'people' - Field failed to execute", results.Errors[0].Message);
        }

        [Fact]
        public void TestErrorFieldNotIncludedInResponseWhenNoErrors()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>();
            var gql = new QueryRequest
            {
                Query = @"{
                    locations { id }
                }"
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.False(results.HasErrors());
            var result = System.Text.Json.JsonSerializer.Serialize(results);
            Assert.DoesNotContain("errors", result);
            Assert.Contains("data", result);
        }

        [Fact]
        public void TestExtensionException()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            var gql = new QueryRequest
            {
                Query = @"{
                    people { error }
                }"
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.True(results.HasErrors());
            Assert.NotNull(results.Errors);
            var error = results.Errors[0];
            Assert.NotNull(error.Extensions);
            Assert.Equal(1, error.Extensions["code"]);
            var result = System.Text.Json.JsonSerializer.Serialize(results);
            Assert.Contains("errors", result);
            Assert.DoesNotContain("data", result);
        }

        [Fact]
        public void MutationReportsError_UnexposedException()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderSchemaOptions { IsDevelopment = false });
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($name: String) {
  addPersonErrorUnexposedException(name: $name)
}
",
                Variables = new QueryVariables {
                    {"name", "Bill"}
                }
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            // error from execution that prevented a valid response, the data entry in the response should be null
            Assert.Null(results.Data);
            Assert.Equal("Field 'addPersonErrorUnexposedException' - Error occurred", results.Errors[0].Message);
        }

        [Fact]
        public void MutationReportsError_UnexposedException_Development()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation AddPerson($name: String) {
  addPersonErrorUnexposedException(name: $name)
}
",
                Variables = new QueryVariables {
                    {"name", "Bill"}
                }
            };

            var testSchema = new TestDataContext();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            // error from execution that prevented a valid response, the data entry in the response should be null
            Assert.Null(results.Data);
            Assert.Equal("Field 'addPersonErrorUnexposedException' - You should not see this message outside of Development", results.Errors[0].Message);
        }

        [Fact]
        public void QueryReportsError_UnexposedException()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderSchemaOptions { IsDevelopment = false });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"{
    people { error_UnexposedException }
}",
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field 'people' - Error occurred", results.Errors[0].Message);
        }

        [Fact]
        public void QueryReportsError_UnexposedException_Development()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"{
    people { error_UnexposedException }
}",
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field 'people' - You should not see this message outside of Development", results.Errors[0].Message);
        }

        [Fact]
        public void QueryReportsError_UnexposedException_WithWhitelist()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderSchemaOptions { IsDevelopment = false });
            schemaProvider.AllowedExceptions.Add(new AllowedException(typeof(Exception)));
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"{
    people { error_UnexposedArgumentException }
}",
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field 'people' - You should not see this message outside of Development", results.Errors[0].Message);
        }

        [Fact]
        public void QueryReportsError_UnexposedException_WithWhitelist_Development()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AllowedExceptions.Add(new AllowedException(typeof(Exception)));
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"{
    people { error_UnexposedArgumentException }
}",
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field 'people' - You should not see this message outside of Development", results.Errors[0].Message);
        }

        [Fact]
        public void QueryReportsError_UnexposedException_Exact_WithWhitelist()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderSchemaOptions { IsDevelopment = false });
            schemaProvider.AllowedExceptions.Add(new AllowedException(typeof(ArgumentException), true));
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"{
    people { error_UnexposedArgumentException }
}",
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field 'people' - You should not see this message outside of Development", results.Errors[0].Message);
        }

        [Fact]
        public void QueryReportsError_UnexposedException_WithWhitelist_Exact_Development()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AllowedExceptions.Add(new AllowedException(typeof(ArgumentException), true));
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"{
    people { error_UnexposedArgumentException }
}",
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field 'people' - You should not see this message outside of Development", results.Errors[0].Message);
        }

        [Fact]
        public void QueryReportsError_UnexposedException_Exact_Mismatch_WithWhitelist()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderSchemaOptions { IsDevelopment = false });
            schemaProvider.AllowedExceptions.Add(new AllowedException(typeof(Exception), true));
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"{
    people { error_UnexposedArgumentException }
}",
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field 'people' - Error occurred", results.Errors[0].Message);
        }

        [Fact]
        public void QueryReportsError_UnexposedException_WithWhitelist_Exact_Mismatch_Development()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
            schemaProvider.AllowedExceptions.Add(new AllowedException(typeof(Exception), true));
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"{
    people { error_UnexposedArgumentException }
}",
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field 'people' - You should not see this message outside of Development", results.Errors[0].Message);
        }

        [Fact]
        public void QueryReportsError_DistinctErrors()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderSchemaOptions { IsDevelopment = false });
            var gql = new QueryRequest
            {
                Query = @"{
    people { error_AggregateException }
}",
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Single(results.Errors);
            Assert.Equal("Field 'people' - Error occurred", results.Errors[0].Message);
        }

        [Fact]
        public void QueryReportsError_AllowedExceptionAttribute()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderSchemaOptions { IsDevelopment = false });
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"{
                    people { error_Allowed }
                }",
            };

            var testSchema = new TestDataContext().FillWithTestData();
            var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
            Assert.NotNull(results.Errors);
            Assert.Equal("Field 'people' - This error is allowed", results.Errors[0].Message);
        }
    }
}