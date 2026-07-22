using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

public class NullResolveAsyncReproTests
{
    public class MyContext
    {
        public List<Widget> Widgets { get; set; } = [];
    }

    public class Widget
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public interface ILookupService { }

    public class LookupService : ILookupService { }

    private static async System.Threading.Tasks.Task<Widget?> LookupAsync(MyContext ctx, int id, ILookupService svc)
    {
        await System.Threading.Tasks.Task.Yield();
        return ctx.Widgets.FirstOrDefault(w => w.Id == id);
    }

    [Fact]
    public async System.Threading.Tasks.Task ResolveAsync_NullResult_ReturnsNullField()
    {
        var schema = SchemaBuilder.FromObject<MyContext>();
        schema
            .Query()
            .AddField("lookupWidget", new { id = ArgumentHelper.Required<int>() }, "desc")
            .ResolveAsync<ILookupService>((ctx, args, svc) => LookupAsync(ctx, args.id, svc)!);

        var gql = new QueryRequest { Query = "{ lookupWidget(id: 999) { id name } }" };

        var context = new MyContext();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<ILookupService>(new LookupService());

        var res = await schema.ExecuteRequestWithContextAsync(gql, context, serviceCollection.BuildServiceProvider(), null);

        Assert.Null(res.Errors);
        Assert.NotNull(res.Data);
        Assert.Null(res.Data!["lookupWidget"]);
    }

    [Fact]
    public async System.Threading.Tasks.Task ResolveAsync_NonNullResult_ReturnsFullObject()
    {
        var schema = SchemaBuilder.FromObject<MyContext>();
        schema
            .Query()
            .AddField("lookupWidget", new { id = ArgumentHelper.Required<int>() }, "desc")
            .ResolveAsync<ILookupService>((ctx, args, svc) => LookupAsync(ctx, args.id, svc)!);

        var gql = new QueryRequest { Query = "{ lookupWidget(id: 1) { id name } }" };

        var context = new MyContext { Widgets = [new Widget { Id = 1, Name = "Widget One" }] };
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<ILookupService>(new LookupService());

        var res = await schema.ExecuteRequestWithContextAsync(gql, context, serviceCollection.BuildServiceProvider(), null);

        Assert.Null(res.Errors);
        Assert.NotNull(res.Data);
        dynamic widget = res.Data!["lookupWidget"]!;
        Assert.Equal(1, widget.id);
        Assert.Equal("Widget One", widget.name);
    }

    [Fact]
    public void Resolve_Sync_NullResult_ReturnsNullField_Baseline()
    {
        var schema = SchemaBuilder.FromObject<MyContext>();
        schema
            .Query()
            .AddField("lookupWidget", new { id = ArgumentHelper.Required<int>() }, "desc")
            .Resolve<ILookupService>((ctx, args, svc) => LookupAsync(ctx, args.id, svc).GetAwaiter().GetResult());

        var gql = new QueryRequest { Query = "{ lookupWidget(id: 999) { id name } }" };

        var context = new MyContext();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<ILookupService>(new LookupService());

        var res = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);

        Assert.Null(res.Errors);
        Assert.NotNull(res.Data);
        Assert.Null(res.Data!["lookupWidget"]);
    }
}
