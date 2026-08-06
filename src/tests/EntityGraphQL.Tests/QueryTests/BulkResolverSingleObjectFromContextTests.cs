using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// A single-object field with a bulk resolver whose per-item resolver is a query on the schema context itself -
/// e.g. <c>.Resolve&lt;MyContext&gt;((p, ctx) =&gt; ctx.Site.Movies.FirstOrDefault(m =&gt; m.DirectorId == p.Id))</c>
/// with a matching <c>ResolveBulk</c>. The second pass replaces the expression with the bulk dictionary lookup,
/// which is a single object, so it must be projected as one - building it as a collection selection asks for
/// <c>Enumerable.Select</c> over a single value ("No generic method 'Select' on type 'System.Linq.Enumerable'
/// is compatible with the supplied type arguments and arguments").
/// Reported as https://github.com/EntityGraphQL/EntityGraphQL/issues/543 - repro shape from
/// https://github.com/soilidokay/GraphqlResolveError.
/// </summary>
public class BulkResolverSingleObjectFromContextTests
{
    private class MovieClean
    {
        public uint Id { get; set; }
        public uint DirectorId { get; set; }
    }

    private class PersonClean
    {
        public uint Id { get; set; }
    }

    private class Site
    {
        public List<MovieClean> Movies { get; set; } = [];
        public List<PersonClean> Peoples { get; set; } = [];
    }

    // the schema context is a provider of "sites", each with its own queryables - as in the report
    private class ContextProvider
    {
        public Site Site1 { get; set; } = new();
        public Site Site2 { get; set; } = new();
    }

    // as their UserService does - takes the context in its constructor
    private class MovieService(ContextProvider ctx)
    {
        public int ItemCalls { get; private set; }
        public int BulkCalls { get; private set; }

        public MovieClean? GetByDirector(uint directorId)
        {
            ItemCalls++;
            return ctx.Site2.Movies.FirstOrDefault(m => m.DirectorId == directorId);
        }

        public IDictionary<uint, MovieClean> GetByDirectors(IEnumerable<uint> directorIds)
        {
            BulkCalls++;
            return ctx.Site2.Movies.Where(m => directorIds.Contains(m.DirectorId)).ToDictionary(m => m.DirectorId, m => m);
        }
    }

    private static (SchemaProvider<ContextProvider> schema, ContextProvider ctx, ServiceProvider sp, MovieService srv) Build()
    {
        var schema = SchemaBuilder.FromObject<ContextProvider>();
        var ctx = new ContextProvider
        {
            Site1 = new Site { Peoples = [new PersonClean { Id = 3 }] },
            Site2 = new Site { Movies = [new MovieClean { Id = 7, DirectorId = 3 }] },
        };
        var srv = new MovieService(ctx);
        var services = new ServiceCollection();
        services.AddSingleton(srv);
        services.AddSingleton(ctx);
        return (schema, ctx, services.BuildServiceProvider(), srv);
    }

    private const string Query = "{ site1 { peoples { id movies2 { id directorId } } } }";

    /// <summary>
    /// The reported shape: both the per-item resolver and the bulk loader take the schema context as their
    /// service.
    /// </summary>
    [Fact]
    public void SingleObjectBulkFieldResolvedFromTheSchemaContext()
    {
        var (schema, ctx, sp, srv) = Build();
        schema
            .Type<PersonClean>()
            .AddField("movies2", "The person's movie")
            .Resolve<ContextProvider>((p, db) => db.Site2.Movies.FirstOrDefault(m => m.DirectorId == p.Id))
            .ResolveBulk<ContextProvider, uint, MovieClean>(
                p => p.Id,
                (ids, db) => db.Site2.Movies.Where(m => ids.Contains(m.DirectorId)).ToDictionary(m => m.DirectorId, m => m)
            );

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = Query }, ctx, sp, null);

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        dynamic site = res.Data!["site1"]!;
        Assert.Equal(7u, site.peoples[0].movies2.id);
        Assert.Equal(3u, site.peoples[0].movies2.directorId);
    }

    /// <summary>Same field, resolved through a separate service that reads the context.</summary>
    [Fact]
    public void SingleObjectBulkFieldResolvedFromAnotherService()
    {
        var (schema, ctx, sp, srv) = Build();
        schema
            .Type<PersonClean>()
            .AddField("movies2", "The person's movie")
            .Resolve<MovieService>((p, movies) => movies.GetByDirector(p.Id))
            .ResolveBulk<MovieService, uint, MovieClean>(p => p.Id, (ids, movies) => movies.GetByDirectors(ids));

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = Query }, ctx, sp, null);

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        dynamic site = res.Data!["site1"]!;
        Assert.Equal(7u, site.peoples[0].movies2.id);
        Assert.Equal(1, srv.BulkCalls);
        Assert.Equal(0, srv.ItemCalls);
    }

    /// <summary>Without the bulk resolver the same field still resolves per item.</summary>
    [Fact]
    public void SingleObjectFieldFromTheSchemaContext_NoBulkResolver()
    {
        var (schema, ctx, sp, _) = Build();
        schema
            .Type<PersonClean>()
            .AddField("movies2", "The person's movie")
            .Resolve<ContextProvider>((p, db) => db.Site2.Movies.FirstOrDefault(m => m.DirectorId == p.Id));

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = Query }, ctx, sp, null);

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        dynamic site = res.Data!["site1"]!;
        Assert.Equal(7u, site.peoples[0].movies2.id);
    }
}
