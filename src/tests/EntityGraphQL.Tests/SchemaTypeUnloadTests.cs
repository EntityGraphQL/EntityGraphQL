using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// A schema's dynamic types (query results, arguments) are built in a collectible assembly of its own, so an app that
/// creates and drops schemas - one per tenant, or per version of a runtime-defined model - can unload each one's types
/// once it is disposed, if its queries are compiled (CacheCompiledDelegates). Nothing that outlives the schema may hold
/// them: the engine's caches are weak.
/// </summary>
public class SchemaTypeUnloadTests
{
    private static readonly string[] queries =
    [
        "{ people { id name manager { name } } }",
        "{ person(id: 1) { name projects { name } } }",
        "{ people(filter: \"name == \\\"A\\\"\") { name } }",
        "{ pagedPeople(first: 1) { edges { cursor node { name } } } }",
        "{ people { pagedProjects(first: 1) { edges { cursor node { name } } } } }",
    ];

    [Fact]
    public void Dropped_schemas_types_are_unloaded()
    {
        var dropped = Enumerable.Range(0, Schemas).Select(i => RunOnNewSchema(queries[i % queries.Length])).ToList();
        CollectUntil(() => dropped.All(d => !d.Schema.IsAlive && !d.ResultType.IsAlive));

        Assert.DoesNotContain(dropped, d => d.Schema.IsAlive);
        AssertUnloaded(dropped.Select(d => d.ResultType));
    }

    // .NET 8 and 9 keep recently compiled methods in a fixed-size cache (System.Linq.Expressions, at most 128 slots),
    // so some recent schemas' types can stay loaded until other methods replace them. A leak would keep them all.
    private const int Schemas = 300;

    private static void AssertUnloaded(IEnumerable<WeakReference> types)
    {
        var loaded = types.Count(t => t.IsAlive);
#if NET10_0_OR_GREATER
        Assert.Equal(0, loaded);
#else
        Assert.InRange(loaded, 0, 128);
#endif
    }

    private static void CollectUntil(Func<bool> done)
    {
        for (var i = 0; i < 10 && !done(); i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Schema, WeakReference ResultType) RunOnNewSchema(string query)
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().ReplaceField("people", ctx => ctx.People, "People").UseFilter();
        schema.Query().AddField("pagedPeople", ctx => ctx.People, "People").UseConnectionPaging();
        schema.Type<Person>().AddField("pagedProjects", p => p.Projects, "Projects").UseConnectionPaging();
        var data = new TestDataContext { People = [new Person { Id = 1, Name = "A", Manager = new Person { Name = "B" }, Projects = [new Project { Name = "P" }] }] };

        // compiled, not interpreted: .NET's expression interpreter keeps every field it reads in a static cache,
        // so an interpreted query's types are never unloaded
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, data, null, null, new ExecutionOptions { CacheCompiledDelegates = true });

        Assert.Null(result.Errors);
        var value = ((IDictionary<string, object?>)result.Data!).Values.Single()!;
        var resultType = (value as IEnumerable<object>)?.First().GetType() ?? value.GetType();
        Assert.True(resultType.IsCollectible);
        // Disposing releases the schema's query cache, whose entries reference it
        schema.Dispose();
        return (new WeakReference(schema), new WeakReference(resultType));
    }
}
