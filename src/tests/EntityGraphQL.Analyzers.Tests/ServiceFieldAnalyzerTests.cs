using System.Threading.Tasks;
using Xunit;

namespace EntityGraphQL.Analyzers.Tests;

public class ServiceFieldAnalyzerTests
{
    /// <summary>Schema types + services every snippet builds on.</summary>
    private const string Preamble =
        @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EntityGraphQL.Schema;
using Microsoft.EntityFrameworkCore;

public class Project { public int Id { get; set; } public string Name { get; set; } = """"; }
public class Ctx { public List<Project> Projects { get; set; } = new(); }
public class UserSvc
{
    public string Get(int id) => """";
    public IDictionary<int, string> GetAll(IEnumerable<int> ids) => new Dictionary<int, string>();
    public Task<string> GetAsync(int id) => Task.FromResult("""");
}
public class MyDb : DbContext { public Task<string> Lookup(int id) => Task.FromResult(""""); }
";

    [Fact]
    public async Task ServiceFieldOnEntityType_WithoutBulk_Reports()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""createdBy"", ""d"").Resolve<UserSvc>((p, srv) => srv.Get(p.Id));
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new ServiceFieldAnalyzer(), "EGQL001");
    }

    [Fact]
    public async Task ServiceFieldOnEntityType_WithBulkResolver_Clean()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""createdBy"", ""d"")
            .Resolve<UserSvc>((p, srv) => srv.Get(p.Id))
            .ResolveBulk<UserSvc, int, string>(p => p.Id, (ids, srv) => srv.GetAll(ids));
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new ServiceFieldAnalyzer());
    }

    [Fact]
    public async Task ServiceFieldOnRootQueryType_Clean()
    {
        // a root field resolves once per request - no N+1
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Query().AddField(""greeting"", ""d"").Resolve<UserSvc>((ctx, srv) => srv.Get(1));
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new ServiceFieldAnalyzer());
    }

    [Fact]
    public async Task ServiceFieldInUpdateQuery_Clean()
    {
        // UpdateQuery configures the root type too - the chain just does not say so
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.UpdateQuery(query => query.AddField(""greeting"", ""d"").Resolve<UserSvc>((ctx, srv) => srv.Get(1)));
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new ServiceFieldAnalyzer());
    }

    [Fact]
    public async Task FieldWithNoService_Clean()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""upperName"", ""d"").Resolve(p => p.Name.ToUpper());
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new ServiceFieldAnalyzer());
    }

    [Fact]
    public async Task ServiceFieldViaUpdateType_Reports()
    {
        // the fluent chain starts at the lambda parameter, not schema.Type<T>()
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.UpdateType<Project>(type => type.AddField(""createdBy"", ""d"").Resolve<UserSvc>((p, srv) => srv.Get(p.Id)));
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new ServiceFieldAnalyzer(), "EGQL001");
    }

    [Fact]
    public async Task AsyncFieldWithDbContextService_ReportsUnsafeService()
    {
        // bulk resolver present so only the thread-safety diagnostic is expected
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""lookup"", ""d"")
            .ResolveAsync<MyDb>((p, db) => db.Lookup(p.Id))
            .ResolveBulk<UserSvc, int, string>(p => p.Id, (ids, srv) => srv.GetAll(ids));
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new ServiceFieldAnalyzer(), "EGQL002");
    }

    [Fact]
    public async Task AsyncFieldWithDbContextService_MaxConcurrencyOne_Clean()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""lookup"", ""d"")
            .ResolveAsync<MyDb>((p, db) => db.Lookup(p.Id), maxConcurrency: 1)
            .ResolveBulk<UserSvc, int, string>(p => p.Id, (ids, srv) => srv.GetAll(ids));
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new ServiceFieldAnalyzer());
    }

    /// <summary>
    /// A limit other than 1 still resolves concurrently, just fewer at a time, so the service is still used
    /// from several threads at once.
    /// </summary>
    [Fact]
    public async Task AsyncFieldWithDbContextService_MaxConcurrencyAboveOne_Reports()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""lookup"", ""d"")
            .ResolveAsync<MyDb>((p, db) => db.Lookup(p.Id), maxConcurrency: 50)
            .ResolveBulk<UserSvc, int, string>(p => p.Id, (ids, srv) => srv.GetAll(ids));
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new ServiceFieldAnalyzer(), "EGQL002");
    }

    /// <summary>
    /// The same field defined over several statements through a stored builder. EGQL001 used to look only at
    /// the statement containing the Resolve call, so a bulk resolver set up on the next line was invisible.
    /// </summary>
    [Fact]
    public async Task ServiceFieldWithBulkResolverOnStoredBuilder_Clean()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        var field = schema.Type<Project>().AddField(""createdBy"", ""d"");
        field.Resolve<UserSvc>((p, srv) => srv.Get(p.Id));
        field.ResolveBulk<UserSvc, int, string>(p => p.Id, (ids, srv) => srv.GetAll(ids));
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new ServiceFieldAnalyzer());
    }

    /// <summary>A same-named method on an unrelated type must not satisfy the rule.</summary>
    [Fact]
    public async Task ServiceFieldWithUnrelatedResolveBulkCall_Reports()
    {
        var source =
            Preamble
            + @"
public class Decoy { public void ResolveBulkSomething() { } }
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        new Decoy().ResolveBulkSomething();
        schema.Type<Project>().AddField(""createdBy"", ""d"").Resolve<UserSvc>((p, srv) => srv.Get(p.Id));
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new ServiceFieldAnalyzer(), "EGQL001");
    }

    [Fact]
    public async Task AsyncFieldWithOrdinaryService_NoUnsafeServiceDiagnostic()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""createdBy"", ""d"")
            .ResolveAsync<UserSvc>((p, srv) => srv.GetAsync(p.Id))
            .ResolveBulk<UserSvc, int, string>(p => p.Id, (ids, srv) => srv.GetAll(ids));
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new ServiceFieldAnalyzer());
    }

    [Fact]
    public async Task NonEntityGraphQLResolveMethod_Ignored()
    {
        // a user's own Resolve<T> method must not be mistaken for the schema-building API
        var source =
            @"
public class Other { public Other Resolve<T>(string s) => this; }
public static class Setup
{
    public static void Build()
    {
        new Other().Resolve<string>(""x"");
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new ServiceFieldAnalyzer());
    }
}
