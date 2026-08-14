using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// A bulk loader can take an <see cref="IFieldSelection"/> and be told what the engine will read off the
/// objects it returns, so it can fetch only that instead of everything.
/// </summary>
public class BulkResolverFieldSelectionTests
{
    private class Address
    {
        public string City { get; set; } = string.Empty;
        public string Postcode { get; set; } = string.Empty;
    }

    private class Owner
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int ManagerId { get; set; }
        public Address Address { get; set; } = new();
    }

    private class Thing
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
    }

    private class Ctx
    {
        public List<Thing> Things { get; set; } = [];
    }

    /// <summary>Records what each load was asked for.</summary>
    private class OwnerLoader
    {
        public List<string> LastPaths { get; private set; } = [];

        public Owner LoadOne(int id) => new() { Id = id, Name = $"owner-{id}" };

        public Owner LoadOne(int id, IFieldSelection selection)
        {
            LastPaths = [.. selection.Paths];
            return new Owner
            {
                Id = id,
                Name = $"owner-{id}",
                Email = $"owner-{id}@example.com",
            };
        }

        public IDictionary<int, Owner> Load(IEnumerable<int> ids, IFieldSelection selection)
        {
            LastPaths = [.. selection.Paths];
            return ids.Distinct()
                .ToDictionary(
                    i => i,
                    i => new Owner
                    {
                        Id = i,
                        Name = $"owner-{i}",
                        Email = $"owner-{i}@example.com",
                        ManagerId = i + 100,
                        Address = new Address { City = "Sydney", Postcode = "2000" },
                    }
                );
        }
    }

    private static (SchemaProvider<Ctx> schema, Ctx ctx, ServiceProvider sp, OwnerLoader loader) Build()
    {
        var schema = SchemaBuilder.FromObject<Ctx>();
        schema.AddType<Address>("Address", "address").AddAllFields();
        schema.AddType<Owner>("Owner", "owner").AddAllFields();
        var loader = new OwnerLoader();
        var ctx = new Ctx { Things = [new Thing { Id = 1, OwnerId = 7 }] };
        var services = new ServiceCollection();
        services.AddSingleton(loader);
        return (schema, ctx, services.BuildServiceProvider(), loader);
    }

    private static void AddOwnerField(SchemaProvider<Ctx> schema, OwnerLoader loader) =>
        schema
            .Type<Thing>()
            .AddField("owner", "The owner")
            .Resolve<OwnerLoader>((t, l) => l.LoadOne(t.OwnerId))
            .ResolveBulk<IFieldSelection, int, Owner>(t => t.OwnerId, (ids, selection) => loader.Load(ids, selection));

    [Fact]
    public void LoaderIsToldTheSelectedFieldsAndTheKey()
    {
        var (schema, ctx, sp, loader) = Build();
        AddOwnerField(schema, loader);

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ things { owner { name } } }" }, ctx, sp, null);

        Assert.Null(res.Errors);
        Assert.Equal(["Name"], loader.LastPaths);
    }

    /// <summary>Reading through a nested object gives its members as dotted paths, not the object alone.</summary>
    [Fact]
    public void NestedObjectIsGivenAsPaths()
    {
        var (schema, ctx, sp, loader) = Build();
        AddOwnerField(schema, loader);

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ things { owner { name address { city } } } }" }, ctx, sp, null);

        Assert.Null(res.Errors);
        Assert.Contains("Address.City", loader.LastPaths);
        Assert.DoesNotContain("Address.Postcode", loader.LastPaths);
    }

    /// <summary>Meta fields are computed from the schema - nothing is read for them.</summary>
    [Fact]
    public void TypenameIsNotAskedFor()
    {
        var (schema, ctx, sp, loader) = Build();
        AddOwnerField(schema, loader);

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ things { owner { name __typename } } }" }, ctx, sp, null);

        Assert.Null(res.Errors);
        Assert.DoesNotContain(loader.LastPaths, p => p.Contains("typename"));
    }

    /// <summary>
    /// One load serves every place the field is selected within a root field, so the loader is told the union
    /// of what those places read - fetching one place's fields would leave the other with nulls. (Root fields
    /// load separately, so they get their own selection each.)
    /// </summary>
    [Fact]
    public void SelectionsServedByOneLoadAreMerged()
    {
        var (schema, ctx, sp, loader) = Build();
        AddOwnerField(schema, loader);

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ things { owner { name } second: owner { email } } }" }, ctx, sp, null);

        Assert.Null(res.Errors);
        Assert.Contains("Name", loader.LastPaths);
        Assert.Contains("Email", loader.LastPaths);
    }

    /// <summary>
    /// The shape a real loader needs: its own service plus the selection, so it can fetch only what will be
    /// read from a second context or a remote service.
    /// </summary>
    [Fact]
    public void LoaderCanTakeItsOwnServiceAndTheSelection()
    {
        var (schema, ctx, sp, loader) = Build();
        schema
            .Type<Thing>()
            .AddField("owner", "The owner")
            .Resolve<OwnerLoader>((t, l) => l.LoadOne(t.OwnerId))
            .ResolveBulk<OwnerLoader, IFieldSelection, int, Owner>(t => t.OwnerId, (ids, l, selection) => l.Load(ids, selection));

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ things { owner { name email } } }" }, ctx, sp, null);

        Assert.Null(res.Errors);
        Assert.Equal(["Name", "Email"], loader.LastPaths);
        dynamic things = res.Data!["things"]!;
        Assert.Equal("owner-7", things[0].owner.name);
    }

    /// <summary>
    /// A per-item resolver is told the same thing - it fetches for one object rather than many, but has the
    /// same reason to fetch only what will be read.
    /// </summary>
    [Fact]
    public void PerItemResolverIsToldTheSelection()
    {
        var (schema, ctx, sp, loader) = Build();
        schema
            .Type<Thing>()
            .AddField("owner", "The owner")
            .Resolve<OwnerLoader, IFieldSelection>((t, l, selection) => l.LoadOne(t.OwnerId, selection));

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ things { owner { name email } } }" }, ctx, sp, null);

        Assert.Null(res.Errors);
        Assert.Equal(["Name", "Email"], loader.LastPaths);
        dynamic things = res.Data!["things"]!;
        Assert.Equal("owner-7", things[0].owner.name);
    }

    /// <summary>
    /// A field the caller skipped is not going to be read, so it is not asked for - the engine decides that,
    /// the same way it decides what to select.
    /// </summary>
    [Theory]
    [InlineData("@skip(if: true)", false)]
    [InlineData("@skip(if: false)", true)]
    [InlineData("@include(if: false)", false)]
    [InlineData("@include(if: true)", true)]
    public void SkippedFieldsAreNotAskedFor(string directive, bool expectedInSelection)
    {
        var (schema, ctx, sp, loader) = Build();
        AddOwnerField(schema, loader);

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = $"{{ things {{ owner {{ name email {directive} }} }} }}" }, ctx, sp, null);

        Assert.Null(res.Errors);
        Assert.Contains("Name", loader.LastPaths);
        Assert.Equal(expectedInSelection, loader.LastPaths.Contains("Email"));
    }

    /// <summary>A scalar field has no sub-selection, so there is nothing to tell a resolver about.</summary>
    [Fact]
    public void ScalarFieldAskingForASelectionIsAClearError()
    {
        var (schema, ctx, sp, loader) = Build();
        schema.Type<Thing>().AddField("label", "A scalar from a service").Resolve<IFieldSelection>((t, selection) => string.Join(",", selection.Paths));

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ things { label } }" }, ctx, sp, null);

        Assert.NotNull(res.Errors);
        Assert.Contains("only supplied to a field that has a selection set", res.Errors![0].Message);
    }

    /// <summary>
    /// The tree carries the schema field each entry came from, so a loader can map a member back to the
    /// schema - the flattened Paths are a convenience over this.
    /// </summary>
    [Fact]
    public void SelectedFieldsCarryTheirSchemaField()
    {
        var (schema, ctx, sp, loader) = Build();
        ISelectedField[] captured = [];
        schema
            .Type<Thing>()
            .AddField("owner", "The owner")
            .Resolve<OwnerLoader>((t, l) => l.LoadOne(t.OwnerId))
            .ResolveBulk<OwnerLoader, IFieldSelection, int, Owner>(
                t => t.OwnerId,
                (ids, l, selection) => Capture(ref captured, selection, l, ids)
            );

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ things { owner { name address { city } } } }" }, ctx, sp, null);

        Assert.Null(res.Errors);
        var name = Assert.Single(captured, f => f.Name == "Name");
        Assert.Equal("name", name.SchemaField!.Name);
        Assert.Empty(name.Fields);

        var address = Assert.Single(captured, f => f.Name == "Address");
        Assert.Equal("address", address.SchemaField!.Name);
        Assert.Equal("City", Assert.Single(address.Fields).Name);
    }

    private static IDictionary<int, Owner> Capture(ref ISelectedField[] captured, IFieldSelection selection, OwnerLoader loader, IEnumerable<int> ids)
    {
        captured = [.. selection.Fields];
        return loader.Load(ids, selection);
    }

    /// <summary>Fields hoisted into a fragment are part of the selection too.</summary>
    [Fact]
    public void FieldsInsideFragmentsAreIncluded()
    {
        var (schema, ctx, sp, loader) = Build();
        AddOwnerField(schema, loader);

        var res = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "query { things { owner { ...OwnerFields } } } fragment OwnerFields on Owner { name email }" },
            ctx,
            sp,
            null
        );

        Assert.Null(res.Errors);
        Assert.Contains("Name", loader.LastPaths);
        Assert.Contains("Email", loader.LastPaths);
    }
}
