using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityGraphQL.Analyzers;

/// <summary>
/// Fixes EGQL004 by changing <c>Resolve(...)</c> to <c>ResolveAsync(...)</c>. The rest of the call -
/// including the service type arguments and the expression - is unchanged, so this is a safe rename.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseResolveAsyncCodeFixProvider)), Shared]
public class UseResolveAsyncCodeFixProvider : CodeFixProvider
{
    private const string Title = "Use ResolveAsync";

    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("EGQL004");

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null)
            return;

        var diagnostic = context.Diagnostics.First();
        // the diagnostic is reported on the method name itself
        var name = root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<SimpleNameSyntax>();
        if (name == null)
            return;

        context.RegisterCodeFix(CodeAction.Create(Title, ct => RenameToResolveAsync(context.Document, root, name, ct), equivalenceKey: Title), diagnostic);
    }

    private static Task<Document> RenameToResolveAsync(Document document, SyntaxNode root, SimpleNameSyntax name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SimpleNameSyntax renamed = name switch
        {
            GenericNameSyntax generic => generic.WithIdentifier(SyntaxFactory.Identifier("ResolveAsync")),
            _ => SyntaxFactory.IdentifierName("ResolveAsync").WithTriviaFrom(name),
        };

        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(name, renamed)));
    }
}
