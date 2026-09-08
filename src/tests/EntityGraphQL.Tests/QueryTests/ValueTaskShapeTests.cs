using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

public class ValueTaskShapeTests
{
    [Fact]
    public void ValueTaskOfListWithSubSelection()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddType<Tag>("Tag", "A tag").AddAllFields();
        schema.Type<Person>().AddField("tags", "").ResolveAsync<VtTagService, List<Tag>>((p, s) => s.GetTagsAsync(p.Id));

        var ctx = new TestDataContext { People = [new Person { Id = 3 }] };
        var services = new ServiceCollection().AddSingleton(new VtTagService()).BuildServiceProvider();

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { tags { name } } }" }, ctx, services, null);
        Assert.True(res.Errors == null, res.Errors == null ? "" : string.Join(" | ", res.Errors.Select(e => e.Message)));
        dynamic people = res.Data!["people"]!;
        Assert.Equal("t3", ((dynamic)((IEnumerable<object>)people[0].tags).First()).name);
    }

    // the bug this PR fixes, through ValueTask instead of Task
    [Fact]
    public void ValueTaskOfListWithAnAsyncFieldOnTheItem()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddType<Tag>("Tag", "A tag").AddAllFields();
        schema.Type<Person>().AddField("tags", "").ResolveAsync<VtTagService, List<Tag>>((p, s) => s.GetTagsAsync(p.Id));
        schema.Type<Tag>().AddField("label", "").ResolveAsync<TagLabelService>((t, s) => s.GetLabelAsync(t.Name));

        var ctx = new TestDataContext { People = [new Person { Id = 3 }] };
        var services = new ServiceCollection().AddSingleton(new VtTagService()).AddSingleton(new TagLabelService()).BuildServiceProvider();

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { tags { name label } } }" }, ctx, services, null);
        Assert.True(res.Errors == null, res.Errors == null ? "" : string.Join(" | ", res.Errors.Select(e => e.Message)));
        dynamic people = res.Data!["people"]!;
        Assert.Equal("label:t3", ((dynamic)((IEnumerable<object>)people[0].tags).First()).label);
    }
}

internal class VtTagService
{
    public async System.Threading.Tasks.ValueTask<List<Tag>> GetTagsAsync(int id)
    {
        await System.Threading.Tasks.Task.Yield();
        return [new Tag { Name = $"t{id}" }];
    }
}
