using System;
using System.Linq;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

/// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
public class SchemaValidateTests
{
    [Fact]
    public void TestMissingTypeError()
    {
        var schema = SchemaBuilder.Create<TestDataContext>();
        schema.Query().AddField("people", ctx => ctx.People, "People");
        var ex = Assert.Throws<EntityGraphQLSchemaException>(() => schema.Validate());
        Assert.Equal("Field 'people' on type 'Query' returns type 'EntityGraphQL.Tests.Person' that is not in the schema", ex.Message);
    }

    [Fact]
    public void TestMissingTypeErrorNonRoot()
    {
        var schema = SchemaBuilder.Create<TestDataContext>();
        schema.Query().AddField("people", ctx => ctx.People, "People");
        schema.AddType<Person>("A person").AddField("tasks", p => p.Tasks, "Tasks");
        var ex = Assert.Throws<EntityGraphQLSchemaException>(() => schema.Validate());
        Assert.Equal("Field 'tasks' on type 'Person' returns type 'EntityGraphQL.Tests.Task' that is not in the schema", ex.Message);
    }

    // ── arguments ─────────────────────────────────────────────────────────────
    // A document variable is built as the dotnet type of the GraphQL type it declares, so an argument declared as
    // a different dotnet type can only work if a conversion exists. AddTypeMapping is one-way and does not make
    // that reverse trip possible - Validate() must catch it rather than leaving it to the first request.

    private readonly struct MyInstant(DateTime utc)
    {
        public DateTime Utc { get; } = utc;
    }

    private static SchemaProvider<TestDataContext> SchemaWithDateScalarRegisteredAs<T>()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.RemoveType("DateTime");
        schema.AddScalarType<T>("Date", "Date scalar");
        schema.AddTypeMapping<DateTime>("Date!");
        schema.AddTypeMapping<DateTime?>("Date");
        schema.Query().AddField("sightings", new { from = default(DateTime) }, (ctx, args) => ctx.People.Where(p => p.Birthday > args.from), "Sightings");
        return schema;
    }

    [Fact]
    public void ArgumentWhoseScalarIsRegisteredAgainstAnotherClrType_FailsValidate()
    {
        // "Date" is registered against MyInstant, DateTime is mapped onto it, and the argument is a DateTime -
        // $from: Date! is built as MyInstant, which cannot become a DateTime
        var schema = SchemaWithDateScalarRegisteredAs<MyInstant>();

        var ex = Assert.Throws<EntityGraphQLSchemaException>(() => schema.Validate());

        Assert.Contains("Field 'sightings' on type 'Query' has argument 'from' of dotnet type 'DateTime'", ex.Message);
        Assert.Contains("GraphQL type 'Date' is registered against dotnet type 'MyInstant'", ex.Message);
        Assert.Contains("Register a custom type converter from 'MyInstant' to 'DateTime'", ex.Message);
    }

    [Fact]
    public void ArgumentWithACustomConverterForThePair_PassesValidate()
    {
        // registering the converter is the consumer-side fix, so Validate() must then pass
        var schema = SchemaWithDateScalarRegisteredAs<MyInstant>();
        schema.AddCustomTypeConverter<MyInstant, DateTime>((instant, _) => instant.Utc);

        schema.Validate();
    }

    [Fact]
    public void ArgumentWhoseScalarIsRegisteredAgainstItsOwnClrType_PassesValidate()
    {
        // the supported way to keep the 5.x name - the scalar is registered against DateTime itself
        SchemaWithDateScalarRegisteredAs<DateTime>().Validate();
    }

    [Fact]
    public void BuiltInTypeMappings_PassValidate()
    {
        // short/long/decimal/... -> Int/Float and byte[] -> String are all reachable via Convert.ChangeType, and
        // an EntityQueryType argument is looked up as String - none of them may fail validation
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema
            .Query()
            .AddField(
                "spread",
                new
                {
                    small = default(short),
                    big = default(long),
                    exact = default(decimal),
                    bytes = Array.Empty<byte>(),
                    maybe = default(short?),
                },
                (ctx, args) => ctx.People,
                "Every built-in mapping as an argument"
            );

        schema.Validate();
    }

    [Fact]
    public void ArgumentTypeNotInTheSchema_FailsValidate()
    {
        // Validate() only walked return types, so a missing argument type used to pass
        var schema = SchemaBuilder.Create<TestDataContext>();
        schema.AddType<Person>("A person").AddField(p => p.Id, "Id");
        schema.Query().AddField("people", new { filter = new PersonFilter() }, (ctx, args) => ctx.People, "People");

        var ex = Assert.Throws<EntityGraphQLSchemaException>(() => schema.Validate());

        Assert.Contains("has argument 'filter' of type", ex.Message);
        Assert.Contains("that is not in the schema", ex.Message);
    }

    private class PersonFilter
    {
        public string? Name { get; set; }
    }
}
