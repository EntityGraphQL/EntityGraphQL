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
    public void Array_Field_KeepsItsItems()
    {
        // an async field returning an array must come back with its items intact - the async resolver walks
        // collections and rebuilds them, so the array must not be flattened, reordered or emptied
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddField("tags", "Tags via array").ResolveAsync<ArrayService>((p, s) => s.GetTagsAsync(p.Id));

        var ctx = new TestDataContext { People = [new Person { Id = 3 }] };
        var services = new ServiceCollection().AddSingleton(new ArrayService()).BuildServiceProvider();

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { tags } }" }, ctx, services, null);

        Assert.Null(res.Errors);
        dynamic people = res.Data!["people"]!;
        var tags = ((IEnumerable<string>)people[0].tags).ToList();
        Assert.Equal(["a3", "b3"], tags);
    }

    [Fact]
    public void TypedList_Field_KeepsItsItems()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddField("notes", "Notes via List<T>").ResolveAsync<TypedListService>((p, s) => s.GetNotesAsync(p.Id));

        var ctx = new TestDataContext { People = [new Person { Id = 9 }] };
        var services = new ServiceCollection().AddSingleton(new TypedListService()).BuildServiceProvider();

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { notes } }" }, ctx, services, null);

        Assert.Null(res.Errors);
        dynamic people = res.Data!["people"]!;
        Assert.Equal(["n9"], ((IEnumerable<string>)people[0].notes).ToList());
    }

    [Fact]
    public void ArrayOfComplexItems_KeepsArrayType()
    {
        // Tag has an object-typed member, so the resolver can not rule out an async value inside it and has
        // to walk the array item by item. The rebuilt collection must still be a Tag[] - anything else is not
        // assignable back to the field
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddType<Tag>("Tag", "A tag").AddAllFields();
        schema.Type<Person>().AddField("tagObjs", "Tags as objects").ResolveAsync<TagArrayService>((p, s) => s.GetTagsAsync(p.Id));

        var ctx = new TestDataContext { People = [new Person { Id = 4 }] };
        var services = new ServiceCollection().AddSingleton(new TagArrayService()).BuildServiceProvider();

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { tagObjs { name } } }" }, ctx, services, null);

        Assert.Null(res.Errors);
        dynamic people = res.Data!["people"]!;
        var names = ((IEnumerable<dynamic>)people[0].tagObjs).Select(t => (string)t.name).ToList();
        Assert.Equal(["t4", "u4"], names);
    }

    [Fact]
    public void ListOfComplexItems_KeepsTypedList()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddType<Tag>("Tag", "A tag").AddAllFields();
        schema.Type<Person>().AddField("tagList", "Tags as a typed list").ResolveAsync<TagListService>((p, s) => s.GetTagsAsync(p.Id));

        var ctx = new TestDataContext { People = [new Person { Id = 6 }] };
        var services = new ServiceCollection().AddSingleton(new TagListService()).BuildServiceProvider();

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { tagList { name } } }" }, ctx, services, null);

        Assert.Null(res.Errors);
        dynamic people = res.Data!["people"]!;
        Assert.Equal(["t6"], ((IEnumerable<dynamic>)people[0].tagList).Select(t => (string)t.name).ToList());
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

// the object-typed member is what stops the resolver ruling out async values in Tag, so arrays/lists of Tag
// have to be walked and rebuilt rather than passed straight through
internal class Tag
{
    public string Name { get; set; } = string.Empty;
    public object? Meta { get; set; }
}

internal class TagArrayService
{
    public async Task<Tag[]> GetTagsAsync(int id)
    {
        await System.Threading.Tasks.Task.Yield();
        return [new Tag { Name = $"t{id}" }, new Tag { Name = $"u{id}" }];
    }
}

internal class TagListService
{
    public async Task<List<Tag>> GetTagsAsync(int id)
    {
        await System.Threading.Tasks.Task.Yield();
        return [new Tag { Name = $"t{id}" }];
    }
}

internal class ArrayService
{
    public async Task<string[]> GetTagsAsync(int id)
    {
        await System.Threading.Tasks.Task.Yield();
        return [$"a{id}", $"b{id}"];
    }
}

internal class TypedListService
{
    public async Task<List<string>> GetNotesAsync(int id)
    {
        await System.Threading.Tasks.Task.Yield();
        return [$"n{id}"];
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
