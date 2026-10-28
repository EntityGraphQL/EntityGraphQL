using System.Runtime.CompilerServices;
using System.Text;
using EntityGraphQL.AspNet.Extensions;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.AspNet.Tests;

/// <summary>
/// The response serializer lives as long as the app, and System.Text.Json caches what it learns of each type on its
/// options. A schema's result types must not end up in that cache, or a disposed schema's types are never unloaded.
/// </summary>
public class ResponseSerializerUnloadTests
{
    private static readonly DefaultGraphQLResponseSerializer serializer = new();

    public class Ctx
    {
        public List<Person> People { get; set; } = [new Person { Name = "Ada", Manager = new Person { Name = "Ben" } }];
    }

    public class Person
    {
        public string Name { get; set; } = "";
        public Person? Manager { get; set; }
    }

    [Fact]
    public async Task A_serialized_result_does_not_keep_a_disposed_schemas_types()
    {
        var dropped = new List<WeakReference>();
        for (var i = 0; i < 300; i++)
        {
            var (json, resultType) = await SerializeOnNewSchema();
            Assert.Equal("{\"data\":{\"people\":[{\"name\":\"Ada\",\"manager\":{\"name\":\"Ben\"}}]}}", json);
            dropped.Add(resultType);
        }
        for (var i = 0; i < 10 && dropped.Any(d => d.IsAlive); i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        // .NET 8 and 9 keep recently compiled methods in a fixed-size cache (System.Linq.Expressions, at most 128
        // slots), so some recent schemas' types can stay loaded until other methods replace them. A leak would keep
        // them all.
        var loaded = dropped.Count(d => d.IsAlive);
#if NET10_0_OR_GREATER
        Assert.Equal(0, loaded);
#else
        Assert.InRange(loaded, 0, 128);
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(string Json, WeakReference ResultType)> SerializeOnNewSchema()
    {
        var schema = SchemaBuilder.FromObject<Ctx>();
        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ people { name manager { name } } }" },
            new Ctx(),
            null,
            null,
            new ExecutionOptions { CacheCompiledDelegates = true }
        );
        var resultType = ((IEnumerable<object>)result.Data!["people"]!).First().GetType();
        using var stream = new MemoryStream();
        await serializer.SerializeAsync(stream, result);
        schema.Dispose();
        return (Encoding.UTF8.GetString(stream.ToArray()), new WeakReference(resultType));
    }
}
