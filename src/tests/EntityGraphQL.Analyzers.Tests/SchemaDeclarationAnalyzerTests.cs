using System.Threading.Tasks;
using Xunit;

namespace EntityGraphQL.Analyzers.Tests;

public class SchemaDeclarationAnalyzerTests
{
    private const string Preamble =
        @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EntityGraphQL;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.Directives;

public class Project { public int Id { get; set; } public string Name { get; set; } = """"; }
public class Ctx { public List<Project> Projects { get; set; } = new(); }
";

    [Fact]
    public async Task FieldNameWithSpace_Reports()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""first name"", p => p.Name, ""d"");
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new SchemaDeclarationAnalyzer(), "EGQL005");
    }

    [Fact]
    public async Task FieldNameWithDash_Reports()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""user-id"", p => p.Id, ""d"");
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new SchemaDeclarationAnalyzer(), "EGQL005");
    }

    [Fact]
    public async Task ValidNames_Clean()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""firstName"", p => p.Name, ""d"");
        schema.Type<Project>().AddField(""_internal"", p => p.Id, ""d"");
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new SchemaDeclarationAnalyzer());
    }

    [Fact]
    public async Task SubscriptionReturningNonObservable_Reports()
    {
        var source =
            Preamble
            + @"
public class Subs
{
    [GraphQLSubscription(""Bad subscription"")]
    public string OnThing(Ctx ctx) => """";
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new SchemaDeclarationAnalyzer(), "EGQL006");
    }

    [Fact]
    public async Task SubscriptionReturningObservable_Clean()
    {
        var source =
            Preamble
            + @"
public class Subs
{
    [GraphQLSubscription(""Good subscription"")]
    public IObservable<Project> OnThing(Ctx ctx) => null!;

    [GraphQLSubscription(""Also good"")]
    public Task<IObservable<Project>> OnThingAsync(Ctx ctx) => null!;
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new SchemaDeclarationAnalyzer());
    }

    [Fact]
    public async Task OneOfTypeWithNonNullableField_Reports()
    {
        var source =
            Preamble
            + @"
[GraphQLOneOf]
public class SearchInput
{
    public int? ById { get; set; }
    public string ByName { get; set; } = """";
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new SchemaDeclarationAnalyzer(), "EGQL007");
    }

    [Fact]
    public async Task OneOfTypeAllNullable_Clean()
    {
        var source =
            Preamble
            + @"
[GraphQLOneOf]
public class SearchInput
{
    public int? ById { get; set; }
    public string? ByName { get; set; }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new SchemaDeclarationAnalyzer());
    }

    [Fact]
    public async Task NonOneOfTypeWithNonNullableField_Clean()
    {
        var source =
            Preamble
            + @"
public class NormalInput
{
    public int ById { get; set; }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new SchemaDeclarationAnalyzer());
    }

    [Fact]
    public async Task SameFieldAddedTwice_Reports()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""slug"", p => p.Name, ""d"");
        schema.Type<Project>().AddField(""slug"", p => p.Name.ToLower(), ""d"");
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new SchemaDeclarationAnalyzer(), "EGQL008");
    }

    [Fact]
    public async Task SameFieldNameOnDifferentTypes_Clean()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""slug"", p => p.Name, ""d"");
        schema.Query().AddField(""slug"", ctx => ""x"", ""d"");
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new SchemaDeclarationAnalyzer());
    }

    [Fact]
    public async Task AddThenReplaceField_Clean()
    {
        var source =
            Preamble
            + @"
public static class Setup
{
    public static void Build(SchemaProvider<Ctx> schema)
    {
        schema.Type<Project>().AddField(""slug"", p => p.Name, ""d"");
        schema.Type<Project>().ReplaceField(""slug"", p => p.Name.ToLower(), ""d"");
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new SchemaDeclarationAnalyzer());
    }

    [Fact]
    public async Task SyncExecuteInAsyncMethod_Reports()
    {
        var source =
            Preamble
            + @"
public static class Runner
{
    public static async Task<object?> Run(SchemaProvider<Ctx> schema, QueryRequest gql, Ctx ctx)
    {
        await Task.Yield();
        var result = schema.ExecuteRequestWithContext(gql, ctx, null, null);
        return result.Data;
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new SchemaDeclarationAnalyzer(), "EGQL009");
    }

    [Fact]
    public async Task SyncExecuteInSyncMethod_Clean()
    {
        var source =
            Preamble
            + @"
public static class Runner
{
    public static object? Run(SchemaProvider<Ctx> schema, QueryRequest gql, Ctx ctx)
    {
        var result = schema.ExecuteRequestWithContext(gql, ctx, null, null);
        return result.Data;
    }
}";
        await AnalyzerHarness.AssertDiagnosticsAsync(source, new SchemaDeclarationAnalyzer());
    }
}
