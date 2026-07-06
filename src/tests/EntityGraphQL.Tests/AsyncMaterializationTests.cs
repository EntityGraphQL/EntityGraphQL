using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// Root list fields on the database-bound pass are materialized in ExecuteExpressionAsync rather than by an
/// in-tree ToList(). When the LINQ provider's query objects implement IAsyncEnumerable&lt;T&gt; (as EF Core's do)
/// the enumeration must go through GetAsyncEnumerator with the request's CancellationToken - these tests use
/// a minimal async-capable provider (the same pattern EF Core testing docs use) to prove that without
/// depending on EF
/// </summary>
public class AsyncMaterializationTests
{
    [Fact]
    public void RootListOverAsyncCapableProvider_IsEnumeratedAsynchronously()
    {
        var schema = SchemaBuilder.FromObject<AsyncProviderContext>();
        var context = new AsyncProviderContext
        {
            Actors = new TestAsyncEnumerable<AsyncActor>([new AsyncActor { Id = 1, Name = "Alec" }, new AsyncActor { Id = 2, Name = "Billy" }]),
        };

        var startCount = TestAsyncEnumerationCounter.AsyncEnumerations;
        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ actors { id name } }" }, context, null, null);

        Assert.Null(res.Errors);
        dynamic actors = res.Data!["actors"]!;
        Assert.Equal(2, actors.Count);
        Assert.Equal("Alec", Enumerable.First(actors).name);
        // the projected query object implements IAsyncEnumerable<T> - the engine must use it, not the sync enumerator
        Assert.True(TestAsyncEnumerationCounter.AsyncEnumerations > startCount, "Expected the root list to be enumerated via IAsyncEnumerable.GetAsyncEnumerator");
    }

    [Fact]
    public async System.Threading.Tasks.Task RootListAsyncEnumeration_ReceivesRequestCancellationToken()
    {
        var schema = SchemaBuilder.FromObject<AsyncProviderContext>();
        using var cts = new CancellationTokenSource();
        var context = new AsyncProviderContext
        {
            // cancels the request token after yielding the first item - if the engine did not pass the
            // request's token through to GetAsyncEnumerator this has no effect and the query succeeds
            Actors = new TestAsyncEnumerable<AsyncActor>([new AsyncActor { Id = 1, Name = "Alec" }, new AsyncActor { Id = 2, Name = "Billy" }]) { CancelAfterFirstItem = cts },
        };
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(context);
        var sp = serviceCollection.BuildServiceProvider();

        var res = await schema.ExecuteRequestAsync(new QueryRequest { Query = "{ actors { id name } }" }, sp, null, new ExecutionOptions(), cts.Token);

        Assert.NotNull(res.Errors);
        Assert.Contains("canceled", res.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RootListOverPlainInMemoryData_StillWorksSynchronously()
    {
        var schema = SchemaBuilder.FromObject<AsyncProviderContext>();
        var context = new AsyncProviderContext { Actors = new List<AsyncActor> { new() { Id = 5, Name = "Sync" } }.AsQueryable() };

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ actors { id name } }" }, context, null, null);

        Assert.Null(res.Errors);
        dynamic actors = res.Data!["actors"]!;
        Assert.Equal(1, actors.Count);
        Assert.Equal("Sync", Enumerable.First(actors).name);
    }
}

internal class AsyncProviderContext
{
    public IQueryable<AsyncActor> Actors { get; set; } = new TestAsyncEnumerable<AsyncActor>(new List<AsyncActor>());
}

internal class AsyncActor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

internal static class TestAsyncEnumerationCounter
{
    private static int asyncEnumerations;
    public static int AsyncEnumerations => asyncEnumerations;

    public static void Increment() => Interlocked.Increment(ref asyncEnumerations);
}

/// <summary>
/// Minimal async-capable IQueryable - query objects created by its provider implement IAsyncEnumerable&lt;T&gt;,
/// mirroring how EF Core's query objects behave
/// </summary>
internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public CancellationTokenSource? CancelAfterFirstItem { get; set; }

    public TestAsyncEnumerable(IEnumerable<T> enumerable)
        : base(enumerable) { }

    public TestAsyncEnumerable(Expression expression)
        : base(expression) { }

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        TestAsyncEnumerationCounter.Increment();
        foreach (var item in this.AsEnumerable())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            CancelAfterFirstItem?.Cancel();
            await System.Threading.Tasks.Task.Yield();
        }
    }
}

internal class TestAsyncQueryProvider<T> : IQueryProvider
{
    private readonly IQueryProvider inner;
    private readonly TestAsyncEnumerable<T> source;

    internal TestAsyncQueryProvider(TestAsyncEnumerable<T> source)
    {
        this.source = source;
        this.inner = new EnumerableQuery<T>(((IQueryable)source).Expression);
    }

    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = expression.Type.GetGenericArguments().FirstOrDefault() ?? typeof(object);
        return (IQueryable)Activator.CreateInstance(typeof(TestAsyncEnumerable<>).MakeGenericType(elementType), expression)!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => new TestAsyncEnumerable<TElement>(expression) { CancelAfterFirstItem = source.CancelAfterFirstItem };

    public object? Execute(Expression expression) => ((IQueryProvider)inner).Execute(expression);

    public TResult Execute<TResult>(Expression expression) => ((IQueryProvider)inner).Execute<TResult>(expression);
}
