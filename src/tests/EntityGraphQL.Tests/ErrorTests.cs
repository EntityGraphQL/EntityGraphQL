using System;
using System.Linq;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

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
            Query =
                @"mutation AddPerson($name: String) {
                    addPersonError(name: $name)
                }",
            Variables = new QueryVariables { { "name", "Bill" } },
        };

        var testSchema = new TestDataContext();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.NotNull(results.Errors);
        // error from execution that prevented a valid response, the data entry in the response should be null
        Assert.Null(results.Data);
        Assert.Equal("Argument name can not be null", results.Errors[0].Message);
        Assert.Equal(["addPersonError"], results.Errors[0].Path);
    }

    [Fact]
    public void QueryReportsError()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"{
    people { error }
}",
        };

        var testSchema = new TestDataContext().FillWithTestData();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.NotNull(results.Errors);
        Assert.Equal("Field failed to execute", results.Errors[0].Message);
        Assert.Equal(1, results.Errors[0]?.Extensions?["code"]);
    }

    [Fact]
    public void TestErrorFieldNotIncludedInResponseWhenNoErrors()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.AddMutationsFrom<PeopleMutations>();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    locations { id }
                }",
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
            Query =
                @"{
                    people { error }
                }",
        };

        var testSchema = new TestDataContext().FillWithTestData();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.True(results.HasErrors());
        Assert.NotNull(results.Errors);
        // from spec errors "bubble up" to nullable field so we expect data to be null people is non-nullable
        Assert.True(results.HasDataKey);
        Assert.Null(results.Data);
        var error = results.Errors[0];
        Assert.NotNull(error.Extensions);
        Assert.Equal(1, error.Extensions["code"]);
        var result = System.Text.Json.JsonSerializer.Serialize(results);
        Assert.Contains("errors", result);
        Assert.Contains("data", result);
    }

    [Fact]
    public void MutationReportsError_UnexposedException()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderSchemaOptions { IsDevelopment = false });
        schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"mutation AddPerson($name: String) {
  addPersonErrorUnexposedException(name: $name)
}
",
            Variables = new QueryVariables { { "name", "Bill" } },
        };

        var testSchema = new TestDataContext();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.NotNull(results.Errors);
        // error from execution that prevented a valid response, the data entry in the response should be null
        Assert.Null(results.Data);
        Assert.Equal("Error occurred", results.Errors[0].Message);
        Assert.Equal(["addPersonErrorUnexposedException"], results.Errors[0].Path);
    }

    [Fact]
    public void MutationReportsError_UnexposedException_Development()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"mutation AddPerson($name: String) {
  addPersonErrorUnexposedException(name: $name)
}
",
            Variables = new QueryVariables { { "name", "Bill" } },
        };

        var testSchema = new TestDataContext();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.NotNull(results.Errors);
        // error from execution that prevented a valid response, the data entry in the response should be null
        Assert.Null(results.Data);
        Assert.Equal("You should not see this message outside of Development", results.Errors[0].Message);
        Assert.Equal(["addPersonErrorUnexposedException"], results.Errors[0].Path);
    }

    [Fact]
    public void QueryReportsError_UnexposedException()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderSchemaOptions { IsDevelopment = false });
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"{
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
            Query =
                @"{
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
            Query =
                @"{
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
            Query =
                @"{
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
            Query =
                @"{
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
            Query =
                @"{
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
            Query =
                @"{
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
            Query =
                @"{
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
            Query =
                @"{
    people { error_AggregateException }
}",
        };

        var testSchema = new TestDataContext().FillWithTestData();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.NotNull(results.Errors);
        Assert.Equal(2, results.Errors.Count);
        Assert.Equal("Field 'people' - Error occurred", results.Errors[0].Message);
        Assert.Equal("Field 'people' - Error occurred", results.Errors[1].Message);
    }

    [Fact]
    public void QueryReportsError_AllowedExceptionAttribute()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>(new SchemaBuilderSchemaOptions { IsDevelopment = false });
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"{
                    people { error_Allowed }
                }",
        };

        var testSchema = new TestDataContext().FillWithTestData();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);
        Assert.NotNull(results.Errors);
        Assert.Equal("Field 'people' - This error is allowed", results.Errors[0].Message);
    }

    [Fact]
    public void MutationExecutionError_SingleField_NonNull()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"mutation AddPerson($name: String) {
                    addPersonError(name: $name)
                }",
            Variables = new QueryVariables { { "name", "Bill" } },
        };

        var testSchema = new TestDataContext();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);

        // contains key 'data' as per spec
        // as addPersonError result is not nullable it rolls up to data
        Assert.True(results.HasDataKey);
        Assert.Null(results.Data);

        Assert.NotNull(results.Errors);
        Assert.Equal($"Argument name can not be null", results.Errors[0].Message);
        Assert.Equal(["addPersonError"], results.Errors[0].Path);
    }

    [Fact]
    public void MutationExecutionError_MultipleFields_NonNull_AliasPath()
    {
        var aliasA = "a";
        var aliasB = "b";
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"mutation AddPerson($name: String) {
                    a: addPersonError(name: $name)
                    b: addPersonError(name: $name)
                }",
            Variables = new QueryVariables { { "name", "Bill" } },
        };

        var testSchema = new TestDataContext();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);

        Assert.True(results.ContainsKey("data"));
        var data = results.Data?.Values;
        // from spec
        // If an error was raised during the execution that prevented a valid response, the "data" entry
        // in the response should be null.
        // both fields are non nullable so no valid response can be returned, hence null
        Assert.Null(data);

        Assert.NotNull(results.Errors);
        Assert.Equal($"Argument name can not be null", results.Errors.First(e => e.Path != null && e.Path.Contains(aliasA)).Message);
        Assert.Equal($"Argument name can not be null", results.Errors.First(e => e.Path != null && e.Path.Contains(aliasB)).Message);
        var paths = results.Errors.Where(e => e.Path != null).SelectMany(e => e.Path!);
        Assert.Equal(2, paths.Count());
        Assert.Contains(aliasA, paths);
        Assert.Contains(aliasB, paths);
    }

    [Fact]
    public void MutationExecutionError_SingleField_Nullable()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"mutation AddPerson($name: String) {
                    addPersonNullableError(name: $name)
                }",
            Variables = new QueryVariables { { "name", "Bill" } },
        };

        var testSchema = new TestDataContext();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);

        Assert.True(results.HasDataKey);
        Assert.NotNull(results.Data);
        Assert.Single(results.Data);
        Assert.All(results.Data.Values, Assert.Null);

        Assert.NotNull(results.Errors);
        var error = results.Errors[0];
        Assert.Equal($"Argument name can not be null", error.Message);
        Assert.Equal(["addPersonNullableError"], error.Path);
    }

    [Fact]
    public void MutationExecutionError_MultipleFields_Nullable_AliasPath()
    {
        var aliasA = "a";
        var aliasB = "b";
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"mutation AddPerson($name: String) {
                    a: addPersonNullableError(name: $name)
                    b: addPersonNullableError(name: $name)
                }",
            Variables = new QueryVariables { { "name", "Bill" } },
        };

        var testSchema = new TestDataContext();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);

        Assert.True(results.ContainsKey("data"));
        var data = results.Data?.Values;
        Assert.NotNull(data);
        Assert.Equal(2, data.Count);
        Assert.All(data, Assert.Null);

        Assert.NotNull(results.Errors);
        Assert.Equal($"Argument name can not be null", results.Errors.First(e => e.Path != null && e.Path.Contains(aliasA)).Message);
        Assert.Equal($"Argument name can not be null", results.Errors.First(e => e.Path != null && e.Path.Contains(aliasB)).Message);
        var paths = results.Errors.Where(e => e.Path != null).SelectMany(e => e.Path!);
        Assert.Equal(2, paths.Count());
        Assert.Equal([aliasA], results.Errors[0].Path);
        Assert.Equal([aliasB], results.Errors[1].Path);
    }

    [Fact]
    public void MutationExecutionError_MultipleFields_NonAliasPath()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();
        schemaProvider.AddMutationsFrom<PeopleMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"mutation AddPerson($name: String) {
                    addPersonError(name: $name)
                    addPersonNullableError(name: $name)
                }",
            Variables = new QueryVariables { { "name", "Bill" } },
        };

        var testSchema = new TestDataContext();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);

        Assert.True(results.ContainsKey("data"));
        var data = results.Data?.Values;
        Assert.NotNull(data);
        Assert.Single(data);
        Assert.Null(data.First());

        Assert.NotNull(results.Errors);
        Assert.Equal($"Argument name can not be null", results.Errors.First(e => e.Path != null && e.Path.Contains("addPersonError")).Message);
        Assert.Equal($"Argument name can not be null", results.Errors.First(e => e.Path != null && e.Path.Contains("addPersonNullableError")).Message);
        var paths = results.Errors.Where(e => e.Path != null).SelectMany(e => e.Path!);
        Assert.Equal(2, paths.Count());
        Assert.Equal(["addPersonError"], results.Errors[0].Path);
        Assert.Equal(["addPersonNullableError"], results.Errors[1].Path);
    }

    private static string ThrowFieldError() => throw new Exception("This field failed");

    private static string ThrowErrorOccurred() => throw new Exception("Error occurred");

    private static string ThrowNonNullError() => throw new Exception("Non-null field failed");

    [Fact]
    public void QueryExecutionPartialResults_MultipleFields()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();

        // Add a field that will succeed
        schemaProvider.Query().AddField("successField", ctx => "Success!", "A field that succeeds");

        // Add a field that will fail
        schemaProvider.Query().AddField("failField", ctx => ThrowFieldError(), "A field that fails");

        // Add another field that will succeed
        schemaProvider.Query().AddField("anotherSuccessField", ctx => 42, "Another field that succeeds");

        var gql = new QueryRequest
        {
            Query =
                @"
            {
                successField
                failField 
                anotherSuccessField
            }",
        };

        var testSchema = new TestDataContext();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);

        // Should have partial data - the successful fields
        Assert.NotNull(results.Data);
        Assert.True(results.Data.ContainsKey("successField"));
        Assert.Equal("Success!", results.Data["successField"]);
        Assert.True(results.Data.ContainsKey("anotherSuccessField"));
        Assert.Equal(42, results.Data["anotherSuccessField"]);

        // Failed field should be null
        Assert.True(results.Data.ContainsKey("failField"));
        Assert.Null(results.Data["failField"]);

        // Should have errors for the failed field
        Assert.NotNull(results.Errors);
        Assert.Single(results.Errors);

        var error = results.Errors[0];
        // The error message will be wrapped by EntityGraphQL
        Assert.NotNull(error.Message);
        Assert.NotNull(error.Path);
        Assert.Single(error.Path);
        Assert.Equal("failField", error.Path[0]);
    }

    [Fact]
    public void QueryExecutionPartialResults_WithAliases()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();

        // Add fields
        schemaProvider.Query().AddField("dataField", ctx => "Some data", "A field that returns data");
        schemaProvider.Query().AddField("errorField", ctx => ThrowErrorOccurred(), "A field that throws an error");

        var gql = new QueryRequest
        {
            Query =
                @"
            {
                firstData: dataField
                problemField: errorField
                secondData: dataField  
            }",
        };

        var testSchema = new TestDataContext();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);

        // Should have partial data with aliases
        Assert.NotNull(results.Data);
        Assert.Equal("Some data", results.Data["firstData"]);
        Assert.Equal("Some data", results.Data["secondData"]);
        Assert.Null(results.Data["problemField"]);

        // Error path should use the alias name
        Assert.NotNull(results.Errors);
        Assert.Single(results.Errors);

        var error = results.Errors[0];
        Assert.NotNull(error.Path);
        Assert.Equal("problemField", error.Path[0]); // Uses alias, not original field name
    }

    [Fact]
    public void QueryExecutionPartialResults_NonNullableField()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestDataContext>();

        // Add a nullable field that succeeds
        schemaProvider.Query().AddField("nullableSuccess", ctx => "Success", "A nullable field that succeeds");

        // Add a non-nullable field that fails
        schemaProvider.Query().AddField("nonNullableFail", ctx => ThrowNonNullError(), "A non-nullable field that fails").IsNullable(false);

        // Add another nullable field that succeeds
        schemaProvider.Query().AddField("anotherNullableSuccess", ctx => "Also success", "Another nullable field that succeeds");

        var gql = new QueryRequest
        {
            Query =
                @"
            {
                nullableSuccess
                nonNullableFail
                anotherNullableSuccess
            }",
        };

        var testSchema = new TestDataContext();
        var results = schemaProvider.ExecuteRequestWithContext(gql, testSchema, null, null);

        // The non-nullable field failure should bubble up and make data null
        // as per GraphQL spec - non-null field errors bubble to the nearest nullable parent
        // However, if other fields are nullable, they may still be included
        // Let's check that we have errors for the non-nullable field
        Assert.True(results.HasErrors());

        // Should still have the error
        Assert.NotNull(results.Errors);
        Assert.Single(results.Errors);

        var error = results.Errors[0];
        // The error message will be wrapped by EntityGraphQL
        Assert.NotNull(error.Message);
        Assert.NotNull(error.Path);
        Assert.Equal("nonNullableFail", error.Path[0]);
    }
}
