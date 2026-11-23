using Microsoft.CodeAnalysis;

namespace EntityGraphQL.Analyzers;

/// <summary>
/// All EntityGraphQL diagnostics in one place.
///
/// Severity policy:
/// - Rules that mirror an exception EntityGraphQL already throws (at schema build or query execution) are
///   Warnings. They cannot be a "false" build break - the application was already broken, we are just
///   reporting it in the editor instead of at run time.
/// - Rules about silent wrong behavior are Warnings.
/// - Rules that are advice (performance, style) are Info, since there are legitimate reasons to ignore them.
/// Any severity can be changed per project in .editorconfig - see the docs.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Correctness = "EntityGraphQL.Correctness";
    private const string Performance = "EntityGraphQL.Performance";

    private const string DocsBase = "https://entitygraphql.github.io/analyzers#";

    /// <summary>EGQL001 - service field on an entity type with no bulk resolver (N+1).</summary>
    internal static readonly DiagnosticDescriptor ServiceFieldWithoutBulkResolver = new(
        "EGQL001",
        "Service field on an entity type has no bulk resolver",
        "Field '{0}' calls a service per item. When '{1}' is returned in a list this runs one service call per row (N+1). Add .ResolveBulk(...) to batch them.",
        Performance,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "A .Resolve<TService>() field on an entity type is executed once per item of any list that selects it. "
            + "Chain .ResolveBulk(...) so the service is called once for the whole list. Ignore this if the type is only "
            + "ever fetched singly or the service batches internally.",
        helpLinkUri: DocsBase + "egql001"
    );

    /// <summary>EGQL002 - a non-thread-safe service (DbContext) used in an async field.</summary>
    internal static readonly DiagnosticDescriptor UnsafeServiceInAsyncField = new(
        "EGQL002",
        "Async field uses a service that is not thread-safe",
        "Field '{0}' resolves asynchronously using '{1}'. An async field on a list runs concurrently per item and '{1}' does not support concurrent use - pass maxConcurrency: 1 or register a ServiceConcurrencyLimit.",
        Correctness,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "ExecutionOptions.MaxQueryConcurrency defaults to 100, so an async field selected on a list resolves "
            + "many items concurrently. A DbContext (or any service documented as not thread-safe) will throw or corrupt "
            + "data when used that way. Limit concurrency for this field or use a factory-created context.",
        helpLinkUri: DocsBase + "egql002"
    );

    /// <summary>EGQL003 - field extension that requires a collection applied to a non-collection field.</summary>
    internal static readonly DiagnosticDescriptor ExtensionRequiresCollection = new(
        "EGQL003",
        "Field extension requires a collection field",
        "'{0}' can only be used on a field that returns a collection, but the field expression returns '{1}'",
        Correctness,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "UseFilter, UseSort, UseOffsetPaging, UseConnectionPaging and UseAggregate all build on top of a "
            + "collection expression. Applying them to a scalar or single-object field throws when the schema is built.",
        helpLinkUri: DocsBase + "egql003"
    );

    /// <summary>EGQL004 - synchronous Resolve used with an expression that returns a Task.</summary>
    internal static readonly DiagnosticDescriptor UseResolveAsyncForAsyncExpression = new(
        "EGQL004",
        "Use ResolveAsync for an expression that returns a Task",
        "Field '{0}' is resolved with Resolve(...) but the expression returns '{1}'. Use ResolveAsync(...) so the result is awaited.",
        Correctness,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A synchronous Resolve() whose expression returns Task<T> makes the field's GraphQL type the Task "
            + "itself. EntityGraphQL rejects this when the schema is built - use ResolveAsync() instead.",
        helpLinkUri: DocsBase + "egql004"
    );

    /// <summary>EGQL005 - a schema name that is not a valid GraphQL name.</summary>
    internal static readonly DiagnosticDescriptor InvalidGraphQLName = new(
        "EGQL005",
        "Name is not a valid GraphQL name",
        "'{0}' is not a valid GraphQL name - names may only contain letters, numbers and underscores",
        Correctness,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "GraphQL names may only contain [_a-zA-Z0-9]. A name with a space, dash or dot produces a schema "
            + "clients cannot query - EntityGraphQL throws for type names and the invalid name reaches the SDL for fields.",
        helpLinkUri: DocsBase + "egql005"
    );

    /// <summary>EGQL006 - a [GraphQLSubscription] method that does not return an IObservable.</summary>
    internal static readonly DiagnosticDescriptor SubscriptionMustReturnObservable = new(
        "EGQL006",
        "Subscription method must return IObservable<T>",
        "Subscription '{0}' returns '{1}' - it must return IObservable<T>, Task<IObservable<T>> or ValueTask<IObservable<T>>",
        Correctness,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A subscription field produces a stream of events, so the method registering it must return an "
            + "observable. EntityGraphQL throws when the schema is built otherwise.",
        helpLinkUri: DocsBase + "egql006"
    );

    /// <summary>EGQL007 - a [GraphQLOneOf] input type with a non-nullable field.</summary>
    internal static readonly DiagnosticDescriptor OneOfFieldsMustBeNullable = new(
        "EGQL007",
        "OneOf input type fields must all be nullable",
        "'{0}' is a OneOf input type so every field must be nullable, but '{1}' is not",
        Correctness,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A OneOf input type accepts exactly one of its fields per request, so every field has to be "
            + "optional. EntityGraphQL throws when the schema is built if any field is non-nullable.",
        helpLinkUri: DocsBase + "egql007"
    );

    /// <summary>EGQL008 - the same field added twice to a type.</summary>
    internal static readonly DiagnosticDescriptor DuplicateFieldName = new(
        "EGQL008",
        "Field is added to the same type twice",
        "Field '{0}' is added to this type more than once - use ReplaceField to redefine an existing field",
        Correctness,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "AddField throws when the field already exists on the type. Use ReplaceField when the intent is "
            + "to redefine a field (including one created by SchemaBuilder.FromObject).",
        helpLinkUri: DocsBase + "egql008"
    );

    /// <summary>EGQL009 - a blocking execute call inside an async method.</summary>
    internal static readonly DiagnosticDescriptor UseAsyncExecuteRequest = new(
        "EGQL009",
        "Use the async execute method inside an async method",
        "'{0}' blocks the calling thread - await '{0}Async' instead",
        Performance,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "The synchronous execute methods block on the async pipeline. Inside an async method, awaiting the "
            + "Async overload avoids tying up a thread for the duration of the query.",
        helpLinkUri: DocsBase + "egql009"
    );

    /// <summary>EGQL010 - blocking on a Task inside a synchronous resolver.</summary>
    internal static readonly DiagnosticDescriptor BlockingAwaitInResolver = new(
        "EGQL010",
        "Resolver blocks on a Task instead of resolving asynchronously",
        "Field '{0}' blocks on a Task ('{1}') inside {2}(...) - use {2}Async(...) so the result is awaited",
        Performance,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Blocking on a Task inside a synchronous resolver ties up the executing thread for the duration of "
            + "the call, and on a list it does so once per item. The Async overload awaits instead. Note that async "
            + "resolvers run concurrently across a list (MaxQueryConcurrency defaults to 100), so the service has to be "
            + "safe to use that way - pass maxConcurrency if it is not.",
        helpLinkUri: DocsBase + "egql010"
    );
}
