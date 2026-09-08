using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

public class AsyncStreamShapeTests
{
    [Fact]
    public void AsyncEnumerableWithSubSelection()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddType<Tag>("Tag", "A tag").AddAllFields();
        schema.Type<Person>().AddField("tags", "").ResolveAsync<TagStreamService, Tag>((p, s) => s.GetTagsAsync(p.Id));

        var ctx = new TestDataContext { People = [new Person { Id = 3 }] };
        var services = new ServiceCollection().AddSingleton(new TagStreamService()).BuildServiceProvider();

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { id tags { name } } }" }, ctx, services, null);
        Assert.Null(res.Errors);
        dynamic people = res.Data!["people"]!;
        Assert.Equal("t3", ((dynamic)((IEnumerable<object>)people[0].tags).First()).name);
    }

    // the shape the PR deferred: async field on the streamed item type
    [Fact]
    public void AsyncEnumerableWithAsyncFieldOnTheItem()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddType<Tag>("Tag", "A tag").AddAllFields();
        schema.Type<Person>().AddField("tags", "").ResolveAsync<TagStreamService, Tag>((p, s) => s.GetTagsAsync(p.Id));
        schema.Type<Tag>().AddField("label", "").ResolveAsync<TagLabelService>((t, s) => s.GetLabelAsync(t.Name));

        var ctx = new TestDataContext { People = [new Person { Id = 3 }] };
        var services = new ServiceCollection().AddSingleton(new TagStreamService()).AddSingleton(new TagLabelService()).BuildServiceProvider();

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { id tags { name label } } }" }, ctx, services, null);
        Assert.True(res.Errors == null, res.Errors == null ? "" : string.Join(" | ", res.Errors.Select(e => e.Message)));
        dynamic people = res.Data!["people"]!;
        var tags = ((IEnumerable<object>)people[0].tags).ToList();
        Assert.Single(tags);
        Assert.Equal("label:t3", ((dynamic)tags[0]).label);
    }
}

internal class TagStreamService
{
    public async IAsyncEnumerable<Tag> GetTagsAsync(int id)
    {
        await System.Threading.Tasks.Task.Yield();
        yield return new Tag { Name = $"t{id}" };
    }
}
