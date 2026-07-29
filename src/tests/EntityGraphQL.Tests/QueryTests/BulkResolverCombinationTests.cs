using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// The problems reported in https://github.com/EntityGraphQL/EntityGraphQL/issues/534 - a bulk resolved
/// field combined with offset paging, nested below another bulk field, and one whose loaded values are
/// lists. The report was against 5.7.2 where the paging case threw a NullReferenceException.
/// </summary>
public class BulkResolverCombinationTests
{
    public class Owner
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class Detail
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class Item
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
    }

    public class Ctx
    {
        public List<Item> Items { get; set; } = [];
    }

    public class Srv
    {
        public int OwnerCalls { get; private set; }
        public int DetailCalls { get; private set; }
        public int ListCalls { get; private set; }

        public Owner GetOwner(int id)
        {
            OwnerCalls++;
            return new Owner { Id = id, Name = $"owner-{id}" };
        }

        public IDictionary<int, Owner> GetOwners(IEnumerable<int> ids)
        {
            OwnerCalls++;
            return ids.Distinct().ToDictionary(i => i, i => new Owner { Id = i, Name = $"owner-{i}" });
        }

        public Detail GetDetail(int id)
        {
            DetailCalls++;
            return new Detail { Id = id, Label = $"detail-{id}" };
        }

        public IDictionary<int, Detail> GetDetails(IEnumerable<int> ids)
        {
            DetailCalls++;
            return ids.Distinct().ToDictionary(i => i, i => new Detail { Id = i, Label = $"detail-{i}" });
        }

        // returns Dictionary<int, IEnumerable<Detail>> - a list-valued bulk resolver
        public IDictionary<int, IEnumerable<Detail>> GetDetailLists(IEnumerable<int> ids)
        {
            ListCalls++;
            return ids.Distinct().ToDictionary(i => i, i => (IEnumerable<Detail>)[new Detail { Id = i, Label = $"detail-{i}" }]);
        }
    }

    private static (SchemaProvider<Ctx> schema, Ctx ctx, IServiceProvider sp, Srv srv) Build()
    {
        var schema = SchemaBuilder.FromObject<Ctx>();
        schema.AddType<Owner>("Owner", "owner").AddAllFields();
        schema.AddType<Detail>("Detail", "detail").AddAllFields();
        var ctx = new Ctx { Items = [new Item { Id = 1, OwnerId = 7 }, new Item { Id = 2, OwnerId = 8 }] };
        var srv = new Srv();
        var services = new ServiceCollection();
        services.AddSingleton(srv);
        services.AddSingleton(ctx);
        return (schema, ctx, services.BuildServiceProvider(), srv);
    }

    /// <summary>
    /// UseOffsetPaging() on the parent list wraps the return type in OffsetPage&lt;T&gt;, which is not a list -
    /// building the bulk key selection through it used to pass a null element type to MakeGenericType.
    /// </summary>
    [Fact]
    public void BulkFieldUnderOffsetPagedParent()
    {
        var (schema, ctx, sp, srv) = Build();
        schema.Query().ReplaceField("items", ctx => ctx.Items.OrderBy(i => i.Id), "paged items").UseOffsetPaging();
        schema
            .Type<Item>()
            .AddField("owner", "owner")
            .Resolve<Srv>((item, s) => s.GetOwner(item.OwnerId))
            .ResolveBulk<Srv, int, Owner>(item => item.OwnerId, (ids, s) => s.GetOwners(ids));

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ items { items { id owner { id name } } totalItems } }" }, ctx, sp, null);
        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        dynamic page = res.Data!["items"]!;
        Assert.Equal("owner-7", page.items[0].owner.name);
        Assert.Equal(1, srv.OwnerCalls); // bulk
    }

    /// <summary>
    /// A bulk field selected inside another bulk (or any service) field's selection. The first pass does not
    /// reach it, so no bulk load is registered for it and it resolves per-item instead of failing with
    /// "The given key 'bulk_Owner.detail' was not present in the dictionary".
    /// </summary>
    [Fact]
    public void BulkFieldInsideAnotherBulkFieldSelection_ResolvesPerItem()
    {
        var (schema, ctx, sp, _) = Build();
        schema
            .Type<Item>()
            .AddField("owner", "owner")
            .Resolve<Srv>((item, s) => s.GetOwner(item.OwnerId))
            .ResolveBulk<Srv, int, Owner>(item => item.OwnerId, (ids, s) => s.GetOwners(ids));
        schema
            .Type<Owner>()
            .AddField("detail", "detail")
            .Resolve<Srv>((owner, s) => s.GetDetail(owner.Id))
            .ResolveBulk<Srv, int, Detail>(owner => owner.Id, (ids, s) => s.GetDetails(ids));

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ items { id owner { id name detail { id label } } } }" }, ctx, sp, null);
        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        dynamic items = res.Data!["items"]!;
        Assert.Equal("detail-7", items[0].owner.detail.label);
    }

    /// <summary>
    /// A bulk resolver whose values are lists: it returns IDictionary&lt;int, IEnumerable&lt;Detail&gt;&gt; while the
    /// field's dotnet return type is promoted to IQueryable&lt;Detail&gt;. Dictionary generic arguments are
    /// invariant, so casting the loaded data to Dictionary&lt;int, IQueryable&lt;Detail&gt;&gt; threw
    /// "Specified cast is not valid" at execution time.
    /// </summary>
    [Fact]
    public void BulkResolverReturningListValues()
    {
        var (schema, ctx, sp, _) = Build();
        schema
            .Type<Item>()
            .AddField("details", "details")
            .Resolve<Srv>((item, s) => new List<Detail> { s.GetDetail(item.OwnerId) })
            .ResolveBulk<Srv, int, IEnumerable<Detail>>(item => item.OwnerId, (ids, s) => s.GetDetailLists(ids));

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ items { id details { id label } } }" }, ctx, sp, null);
        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        dynamic items = res.Data!["items"]!;
        Assert.Equal("detail-7", items[0].details[0].label);
    }

    /// <summary>
    /// A bulk resolver needs a resolver to bulk load for - the per-item one runs whenever the bulk load can
    /// not (e.g. below another service field). Defining only ResolveBulk used to throw a
    /// NullReferenceException from inside the schema building call.
    /// </summary>
    [Fact]
    public void BulkResolverWithoutResolver_ThrowsAtSchemaBuild()
    {
        var (schema, _, _, _) = Build();
        var ex = Assert.Throws<EntityGraphQLSchemaException>(() =>
            schema.Type<Owner>().AddField("detail", "detail").ResolveBulk<Srv, int, Detail>(owner => owner.Id, (ids, s) => s.GetDetails(ids))
        );
        Assert.Contains("has a bulk resolver but no resolver", ex.Message);
    }
}
