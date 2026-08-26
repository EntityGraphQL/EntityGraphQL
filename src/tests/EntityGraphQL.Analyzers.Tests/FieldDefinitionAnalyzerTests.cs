using System.Threading.Tasks;
using Xunit;

namespace EntityGraphQL.Analyzers.Tests;

public class FieldDefinitionAnalyzerTests
{
    private const string Preamble =
        @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;

public class Task2 { public int Id { get; set; } }
public class Project { public int Id { get; set; } public string Name { get; set; } = """"; public List<Task2> Tasks { get; set; } = new(); }
public class Ctx { public List<Project> Projects { get; set; } = new(); }
public class UserSvc
{
    public Task<string> GetAsync(int id) => Task.FromResult("""");
    public string Get(int id) => """";
    public Task<IDictionary<int, string>> GetAllAsync(IEnumerable<int> ids) => Task.FromResult<IDictionary<int, string>>(new Dictionary<int, string>());
}
";

    [Fact]
    public async Task UseFilterOnScalarField_Reports()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""name"", p => p.Name, ""d"").UseFilter();
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new FieldDefinitionAnalyzer(), "EGQL003");
    }

    [Fact]
    public async Task UseFilterOnCollectionField_Clean()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""tasks"", p => p.Tasks, ""d"").UseFilter();
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new FieldDefinitionAnalyzer());
    }

    [Fact]
    public async Task UseOffsetPagingOnSingleObjectField_Reports()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Query().AddField(""firstProject"", ctx => ctx.Projects.First(), ""d"").UseOffsetPaging();
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new FieldDefinitionAnalyzer(), "EGQL003");
    }

    [Fact]
    public async Task UseConnectionPagingOnQueryableField_Clean()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Query().AddField(""projects"", ctx => ctx.Projects.AsQueryable(), ""d"").UseConnectionPaging();
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new FieldDefinitionAnalyzer());
    }

    [Fact]
    public async Task SyncResolveWithTaskExpression_Reports()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""createdBy"", ""d"").Resolve<UserSvc>((p, srv) => srv.GetAsync(p.Id));
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new FieldDefinitionAnalyzer(), "EGQL004");
    }

    [Fact]
    public async Task ResolveAsyncWithTaskExpression_Clean()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""createdBy"", ""d"").ResolveAsync<UserSvc>((p, srv) => srv.GetAsync(p.Id));
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new FieldDefinitionAnalyzer());
    }

    [Fact]
    public async Task SyncResolveWithSyncExpression_Clean()
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
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new FieldDefinitionAnalyzer());
    }

    [Fact]
    public async Task UseOffsetPagingOnOrderedQueryable_Clean()
    {
        // regression: IOrderedQueryable<T>/IOrderedEnumerable<T> are collections. Roslyn only sets
        // SpecialType on the unbound definition, so the interface walk has to check OriginalDefinition
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Query().AddField(""projects"", ctx => ctx.Projects.AsQueryable().OrderBy(p => p.Name), ""d"").UseOffsetPaging();
        schema.Query().AddField(""sorted"", ctx => ctx.Projects.OrderBy(p => p.Name), ""d"").UseFilter();
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new FieldDefinitionAnalyzer());
    }

    [Theory]
    [InlineData(@"srv.GetAsync(p.Id).Result")]
    [InlineData(@"srv.GetAsync(p.Id).GetAwaiter().GetResult()")]
    [InlineData(@"srv.GetAsync(p.Id).ConfigureAwait(false).GetAwaiter().GetResult()")]
    public async Task ResolveBlockingOnTask_Reports(string body)
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""owner"", ""d"").Resolve<UserSvc>((p, srv) => "
            + body
            + @");
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new FieldDefinitionAnalyzer(), "EGQL010");
    }

    [Fact]
    public async Task BlockingInASeparateMethod_Clean()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""owner"", ""d"").Resolve<UserSvc>((p, srv) => Blocking(srv, p.Id));
    }

    private static string Blocking(UserSvc srv, int id)
    {
        var task = srv.GetAsync(id);
        task.Wait();
        return task.Result;
    }
}";
        // the blocking happens in a separate method, not in the lambda - nothing to report
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new FieldDefinitionAnalyzer());
    }

    [Fact]
    public async Task ResolveBulkBlockingOnTask_Reports()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""owner"", ""d"")
            .Resolve<UserSvc>((p, srv) => srv.Get(p.Id))
            .ResolveBulk<UserSvc, int, string>(p => p.Id, (ids, srv) => srv.GetAllAsync(ids).Result);
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new FieldDefinitionAnalyzer(), "EGQL010");
    }

    [Fact]
    public async Task ResolveAsyncAwaitingTask_Clean()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""owner"", ""d"").ResolveAsync<UserSvc>((p, srv) => srv.GetAsync(p.Id));
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new FieldDefinitionAnalyzer());
    }

    [Fact]
    public async Task ResolveWithNoTask_Clean()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""owner"", ""d"").Resolve<UserSvc>((p, srv) => srv.Get(p.Id));
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new FieldDefinitionAnalyzer());
    }
}
