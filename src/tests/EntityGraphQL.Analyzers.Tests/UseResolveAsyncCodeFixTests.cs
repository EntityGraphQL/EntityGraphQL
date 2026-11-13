using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace EntityGraphQL.Analyzers.Tests;

public class UseResolveAsyncCodeFixTests
{
    private const string Preamble =
        @"
using System.Collections.Generic;
using System.Threading.Tasks;
using EntityGraphQL.Schema;

public class Project { public int Id { get; set; } }
public class Ctx { public List<Project> Projects { get; set; } = new(); }
public class UserSvc { public Task<string> GetAsync(int id) => Task.FromResult(""""); }
";

    [Fact]
    public async Task Fix_RewritesResolveToResolveAsync_KeepingServiceTypeArgument()
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

        var fixedSource = await ApplyFixAsync(source, new FieldDefinitionAnalyzer(), new UseResolveAsyncCodeFixProvider());

        Assert.Contains(".ResolveAsync<UserSvc>((p, srv) => srv.GetAsync(p.Id))", fixedSource);
        Assert.DoesNotContain(".Resolve<UserSvc>(", fixedSource);
        // the fixed code no longer reports the diagnostic
        await AnalyzerHarness.AssertDiagnosticsAsync(fixedSource, new FieldDefinitionAnalyzer());
    }

    /// <summary>
    /// Runs the analyzer over an in-memory workspace document, applies the single offered fix and returns
    /// the resulting source.
    /// </summary>
    private static async Task<string> ApplyFixAsync(string source, DiagnosticAnalyzer analyzer, CodeFixProvider codeFix)
    {
        using var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
        var project = workspace
            .AddProject("FixTest", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(AnalyzerHarness.References);
        var document = project.AddDocument("Test.cs", SourceText.From(source));

        var compilation = await document.Project.GetCompilationAsync();
        var diagnostics = await compilation!.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();
        var diagnostic = Assert.Single(diagnostics.Where(d => codeFix.FixableDiagnosticIds.Contains(d.Id)));

        CodeAction? registered = null;
        var context = new CodeFixContext(document, diagnostic, (action, _) => registered = action, CancellationToken.None);
        await codeFix.RegisterCodeFixesAsync(context);
        Assert.NotNull(registered);

        var operations = await registered!.GetOperationsAsync(CancellationToken.None);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var text = await changed.GetDocument(document.Id)!.GetTextAsync();
        return text.ToString();
    }
}
