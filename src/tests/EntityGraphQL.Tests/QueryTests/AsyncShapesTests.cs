using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

public class AsyncShapesTests
{
    [Fact]
    public void ValueTask_Generic_Field_Is_Resolved()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddField("ageVt", "Age via ValueTask").ResolveAsync<VtAgeService, int>((p, s) => s.GetAgeAsync(p.Birthday));

        var ctx = new TestDataContext { People = new List<Person> { new Person { Birthday = DateTime.UtcNow.AddYears(-3) } } };
        var services = new ServiceCollection().AddSingleton(new VtAgeService()).BuildServiceProvider();

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { ageVt } }" }, ctx, services, null);
        Assert.Null(res.Errors);
        dynamic people = res.Data!["people"]!;
        Assert.IsType<int>(people[0].ageVt);
        Assert.InRange((int)people[0].ageVt, 1, 200);
    }

    [Fact]
    public void IAsyncEnumerable_Field_Is_Buffered_To_List()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        // Add the field that returns IAsyncEnumerable directly (no service dependency)
        schema.Type<Person>().AddField("tickets", "Async stream of ints").ResolveAsync<StreamService, int>((p, s) => s.GetStreamAsync(p.Id));

        var ctx = new TestDataContext { People = new List<Person> { new() { Id = 5 } } };
        var services = new ServiceCollection().AddSingleton(new StreamService()).BuildServiceProvider();

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { id tickets } }" }, ctx, services, null);

        Assert.Null(res.Errors);
        Assert.NotNull(res.Data);
        dynamic people = res.Data!["people"]!;
        var list = (IEnumerable<int>)people[0].tickets;
        Assert.Equal(3, list.Count());
        Assert.Equal(5, list.ElementAt(0));
        Assert.Equal(6, list.ElementAt(1));
        Assert.Equal(7, list.ElementAt(2));
    }
}

internal class VtAgeService
{
    public async ValueTask<int> GetAgeAsync(DateTime? birthday)
    {
        await System.Threading.Tasks.Task.Yield();
        return birthday.HasValue ? (int)((DateTime.UtcNow - birthday.Value).TotalDays / 365) : 0;
    }
}

internal class StreamService
{
    public async IAsyncEnumerable<int> GetStreamAsync(int id)
    {
        yield return id;
        await System.Threading.Tasks.Task.Delay(0);
        yield return id + 1;
        await System.Threading.Tasks.Task.Delay(0);
        yield return id + 2;
    }
}
