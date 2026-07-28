using System;
using System.Globalization;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests;

public class ArgumentErrorMessageTests
{
    /// <summary>
    /// Stand in for a CLR type (e.g. NodaTime's Instant) that a scalar is registered against while another CLR
    /// type is mapped onto the same GQL type name with AddTypeMapping.
    /// </summary>
    public readonly struct MyInstant(DateTime utc)
    {
        public DateTime Utc { get; } = utc;
    }

#nullable disable
    internal class HistoricalSightingArgs : OffsetArgs
    {
        public RequiredField<Guid> FloorId { get; set; }
        public RequiredField<DateTime> From { get; set; }
        public RequiredField<DateTime> To { get; set; }
    }

#nullable enable

    private static SchemaProvider<TestDataContext> BuildSchema(bool isDevelopment = true)
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>(new SchemaProviderOptions { IsDevelopment = isDevelopment });
        schema
            .Query()
            .AddField("historicalSightings", new HistoricalSightingArgs(), "Historical sightings")
            .Resolve((db, args) => db.Projects.Where(p => p.Id > 0).Skip(args.Skip ?? 0).Take(args.Take ?? 10).ToList());
        return schema;
    }

    /// <summary>
    /// Registers "Date" against MyInstant and maps DateTime onto the same GQL type name, so a variable declared
    /// as Date! materializes as a MyInstant while the field's argument is a DateTime.
    /// </summary>
    private static SchemaProvider<TestDataContext> BuildSchemaWithMismatchedDateScalar(bool isDevelopment)
    {
        var schema = BuildSchema(isDevelopment);
        schema.AddCustomTypeConverter(
            (TypeConverterTryFrom<string>)(
                (string str, Type toType, ISchemaProvider _, out object? result) =>
                {
                    if (toType == typeof(MyInstant) || toType == typeof(MyInstant?))
                    {
                        result = new MyInstant(DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal));
                        return true;
                    }
                    result = null;
                    return false;
                }
            )
        );
        schema.AddScalarType<MyInstant>("Date", "Date/time scalar");
        schema.RemoveType("DateTime");
        schema.AddTypeMapping<DateTime>("Date!");
        schema.AddTypeMapping<DateTime?>("Date");
        return schema;
    }

    private static QueryRequest HistoricalSightingsQuery() =>
        new()
        {
            Query =
                @"query GetHistoricalSightings($floorId: ID!, $from: Date!, $to: Date!) {
                    historicalSightings(floorId: $floorId, from: $from, to: $to) { id name }
                }",
            Variables = new QueryVariables
            {
                { "floorId", "cccccccc-bbbb-4444-1111-ccddeeff0033" },
                { "from", "2026-05-03T22:00:00.000Z" },
                { "to", "2026-05-03T23:00:00.000Z" },
            },
        };

    [Fact]
    public void ArgumentsFromAnArgsClassUseTheSchemaFieldNamer()
    {
        var sdl = BuildSchema().ToGraphQLSchemaString();
        Assert.Contains("floorId: ID!", sdl);
        Assert.Contains("from: DateTime!", sdl);
        Assert.Contains("to: DateTime!", sdl);
    }

    [Fact]
    public void MissingRequiredArgumentFromAnArgsClassReportsTheSchemaArgumentName()
    {
        var gql = new QueryRequest { Query = @"{ historicalSightings { id } }" };

        var result = BuildSchema().ExecuteRequestWithContext(gql, new TestDataContext().FillWithTestData(), null, null);

        Assert.NotNull(result.Errors);
        // the dotnet properties are FloorId/From/To - each must be reported once, by its schema name
        Assert.Equal(3, result.Errors.Count);
        Assert.Contains("Field 'historicalSightings' - missing required argument 'floorId'", result.Errors.Select(e => e.Message));
        Assert.Contains("Field 'historicalSightings' - missing required argument 'from'", result.Errors.Select(e => e.Message));
        Assert.Contains("Field 'historicalSightings' - missing required argument 'to'", result.Errors.Select(e => e.Message));
    }

    [Fact]
    public void ArgumentBuildFailureCarriesTheCauseInDevelopment()
    {
        var schema = BuildSchemaWithMismatchedDateScalar(isDevelopment: true);

        var result = schema.ExecuteRequestWithContext(HistoricalSightingsQuery(), new TestDataContext().FillWithTestData(), null, null);

        Assert.NotNull(result.Errors);
        var expectedPrefix = "Variable or value used for argument 'from' does not match argument type 'Date!' on field 'historicalSightings' - ";
        var message = Assert.Single(result.Errors.Select(e => e.Message), m => m.StartsWith(expectedPrefix));
        // the GQL types alone say nothing - the cause is a MyInstant value that can not become a DateTime
        Assert.Contains("IConvertible", message);
    }

    [Fact]
    public void ArgumentBuildFailureHidesTheCauseWhenNotInDevelopment()
    {
        var schema = BuildSchemaWithMismatchedDateScalar(isDevelopment: false);

        var result = schema.ExecuteRequestWithContext(HistoricalSightingsQuery(), new TestDataContext().FillWithTestData(), null, null);

        Assert.NotNull(result.Errors);
        Assert.Contains("Variable or value used for argument 'from' does not match argument type 'Date!' on field 'historicalSightings'", result.Errors.Select(e => e.Message));
    }
}
