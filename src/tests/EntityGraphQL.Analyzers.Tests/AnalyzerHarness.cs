using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using EntityGraphQL.Schema;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace EntityGraphQL.Analyzers.Tests;

/// <summary>
/// Compiles a source snippet in memory against the real EntityGraphQL (and EF Core) assemblies and runs an
/// analyzer over it. Deliberately does not use Microsoft.CodeAnalysis.Testing - building the compilation
/// directly keeps the tests fast, offline and free of reference-assembly downloads.
/// </summary>
internal static class AnalyzerHarness
{
    private static readonly ImmutableArray<MetadataReference> references = BuildReferences();

    /// <summary>The metadata references test compilations are built against.</summary>
    internal static ImmutableArray<MetadataReference> References => references;

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        // the runtime's reference set (everything in the test host's probing path), plus the assemblies the
        // snippets bind against explicitly - those are named here rather than discovered, because a lazily
        // loaded assembly would otherwise be missing from the compilation
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
        {
            foreach (var path in trusted.Split(System.IO.Path.PathSeparator))
                paths.Add(path);
        }
        foreach (var assembly in new[] { typeof(SchemaProvider<>).Assembly, typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly, typeof(object).Assembly })
        {
            if (!string.IsNullOrEmpty(assembly.Location))
                paths.Add(assembly.Location);
        }

        // one reference per assembly name - duplicates in the probing path are an ambiguity error
        return
        [
            .. paths
                .Where(System.IO.File.Exists)
                .GroupBy(p => System.IO.Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase)
                .Select(g => (MetadataReference)MetadataReference.CreateFromFile(g.First())),
        ];
    }

    /// <summary>
    /// Run <paramref name="analyzer"/> over <paramref name="source"/> and return the diagnostics it reported.
    /// Fails the test if the snippet itself does not compile, so a typo cannot masquerade as "no diagnostics".
    /// </summary>
    internal static async Task<IReadOnlyList<Diagnostic>> GetDiagnosticsAsync(string source, DiagnosticAnalyzer analyzer)
    {
        var compilation = CSharpCompilation.Create(
            "AnalyzerTest",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
        );

        var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(compileErrors.Count == 0, "Test source failed to compile:\n" + string.Join("\n", compileErrors.Select(e => e.ToString())));

        var withAnalyzers = compilation.WithAnalyzers([analyzer]);
        return [.. await withAnalyzers.GetAnalyzerDiagnosticsAsync()];
    }

    /// <summary>Assert that exactly the given diagnostic ids were reported, in no particular order.</summary>
    internal static async Task AssertDiagnosticsAsync(string source, DiagnosticAnalyzer analyzer, params string[] expectedIds)
    {
        var diagnostics = await GetDiagnosticsAsync(source, analyzer);
        var actualIds = diagnostics.Select(d => d.Id).OrderBy(id => id).ToArray();
        Assert.Equal([.. expectedIds.OrderBy(id => id)], actualIds);
    }
}
