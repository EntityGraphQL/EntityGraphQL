using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using EntityGraphQL.AspNet.Extensions;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.AspNet.Tests;

/// <summary>
/// End-to-end coverage for serializing polymorphic (interface/union) query results through
/// <see cref="DefaultGraphQLResponseSerializer"/>.
///
/// EntityGraphQL projects an interface selection into a base dynamic type with derived dynamic types
/// per concrete implementation (see ExpressionUtil.BuildTypeChecks). The result collection is statically
/// typed as the base dynamic type while holding derived instances at runtime. System.Text.Json serializes
/// by the *declared* type, so without <see cref="RuntimeTypeJsonConverter"/> every field selected inside an
/// inline fragment is silently dropped. These tests lock that behavior in so the converter cannot be
/// removed without a failing test explaining why it exists.
/// </summary>
public class InterfaceSerializationTests
{
    public abstract class Character
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class Human : Character
    {
        public int TotalCredits { get; set; }
    }

    public class Droid : Character
    {
        public string PrimaryFunction { get; set; } = "";
    }

    public class StarWarsContext
    {
        public IList<Character> Characters { get; set; } = [];
    }

    private static QueryResult ExecuteInterfaceQuery()
    {
        var schema = SchemaBuilder.FromObject<StarWarsContext>();
        schema.AddType<Human>("Human").AddAllFields().ImplementAllBaseTypes();
        schema.AddType<Droid>("Droid").AddAllFields().ImplementAllBaseTypes();

        var context = new StarWarsContext
        {
            Characters =
            [
                new Human
                {
                    Id = 1,
                    Name = "Luke",
                    TotalCredits = 100,
                },
                new Droid
                {
                    Id = 2,
                    Name = "R2-D2",
                    PrimaryFunction = "Astromech",
                },
            ],
        };

        var gql = new QueryRequest
        {
            Query =
                @"{ characters { __typename name
                    ... on Human { totalCredits }
                    ... on Droid { primaryFunction }
                } }",
        };
        var result = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.False(result.HasErrors());
        return result;
    }

    private static string Serialize(QueryResult result, JsonSerializerOptions? options)
    {
        var serializer = options == null ? new DefaultGraphQLResponseSerializer() : new DefaultGraphQLResponseSerializer(options);
        using var stream = new MemoryStream();
        serializer.SerializeAsync(stream, result).GetAwaiter().GetResult();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public void InterfaceResult_IncludesInlineFragmentFields_WithConverter()
    {
        var json = Serialize(ExecuteInterfaceQuery(), options: null);

        // base fields
        Assert.Contains("\"name\":\"Luke\"", json);
        Assert.Contains("\"name\":\"R2-D2\"", json);
        // type-specific fields selected via inline fragments — only present because RuntimeTypeJsonConverter
        // serializes the runtime (derived) dynamic type rather than the declared base dynamic type
        Assert.Contains("\"totalCredits\":100", json);
        Assert.Contains("\"primaryFunction\":\"Astromech\"", json);
    }

    [Fact]
    public void InterfaceResult_DropsInlineFragmentFields_WithoutConverter()
    {
        // Remove the converter to demonstrate it is load-bearing: native System.Text.Json serializes the
        // collection elements by their declared base dynamic type and truncates the derived fields.
        var options = DefaultGraphQLResponseSerializer.CreateDefaultOptions();
        var converter = options.Converters.FirstOrDefault(c => c is RuntimeTypeJsonConverter);
        Assert.NotNull(converter);
        options.Converters.Remove(converter!);

        var json = Serialize(ExecuteInterfaceQuery(), options);

        // base fields still serialize
        Assert.Contains("\"name\":\"Luke\"", json);
        // derived fields are dropped by native System.Text.Json
        Assert.DoesNotContain("totalCredits", json);
        Assert.DoesNotContain("primaryFunction", json);
    }
}
