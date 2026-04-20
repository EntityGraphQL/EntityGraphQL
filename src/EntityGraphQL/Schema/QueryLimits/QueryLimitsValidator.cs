using System.Collections.Generic;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Schema.QueryLimits;

/// <summary>
/// Enforces depth, node-count, and alias-count limits on a parsed GraphQL document, and delegates
/// complexity calculation to <see cref="IQueryComplexityAnalyzer"/>. Called pre-execution so exceeded
/// limits abort before any resolver runs.
///
/// Runs once per request and produces a <see cref="EntityGraphQLException"/> with
/// <see cref="GraphQLErrorCategory.DocumentError"/> on the first limit hit.
/// </summary>
internal static class QueryLimitsValidator
{
    internal static void Validate(GraphQLDocument document, string? operationName, QueryVariables? variables, ExecutionOptions options)
    {
        var depthLimit = Nz(options.MaxQueryDepth);
        var nodeLimit = Nz(options.MaxQueryNodes);
        var aliasLimit = Nz(options.MaxFieldAliases);
        var complexityLimit = Nz(options.MaxQueryComplexity);

        if (depthLimit is null && nodeLimit is null && aliasLimit is null && complexityLimit is null)
            return;

        var op = string.IsNullOrEmpty(operationName) ? (document.Operations.Count > 0 ? document.Operations[0] : null) : document.Operations.Find(o => o.Name == operationName);
        if (op == null)
            return;

        if (depthLimit is not null || nodeLimit is not null || aliasLimit is not null)
        {
            var state = new WalkState(document.Fragments, depthLimit, nodeLimit, aliasLimit);
            foreach (var field in op.QueryFields)
                Walk(field, 1, ref state);
        }

        if (complexityLimit is int cLimit)
        {
            var analyzer = options.QueryComplexityAnalyzer ?? new DefaultQueryComplexityAnalyzer();
            var cost = analyzer.CalculateComplexity(document, operationName, variables, options);
            if (cost > cLimit)
                throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Query complexity {cost} exceeds maximum allowed complexity {cLimit}");
        }
    }

    private static void Walk(BaseGraphQLField field, int depth, ref WalkState state)
    {
        if (field is GraphQLFragmentSpreadField spread)
        {
            if (!state.Fragments.TryGetValue(spread.Name, out var fragment))
                return;
            if (!state.MarkVisitedFragment(spread.Name))
                return;
            foreach (var child in fragment.QueryFields)
                Walk(child, depth, ref state);
            return;
        }

        if (field is GraphQLInlineFragmentField inline)
        {
            foreach (var child in inline.QueryFields)
                Walk(child, depth, ref state);
            return;
        }

        state.CountNode();
        if (field.Name != field.SchemaName)
            state.CountAlias();

        if (state.DepthLimit is int d && depth > d)
            WalkState.Fail($"Query exceeds maximum allowed depth of {d}");

        if (field.QueryFields.Count > 0)
        {
            foreach (var child in field.QueryFields)
                Walk(child, depth + 1, ref state);
        }
    }

    private static int? Nz(int? v) => v is null || v.Value <= 0 ? null : v;

    private struct WalkState
    {
        public readonly IReadOnlyDictionary<string, GraphQLFragmentStatement> Fragments;
        public readonly int? DepthLimit;
        public readonly int? NodeLimit;
        public readonly int? AliasLimit;
        private HashSet<string>? visitedFragments;
        private int nodeCount;
        private int aliasCount;

        public WalkState(IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments, int? depthLimit, int? nodeLimit, int? aliasLimit)
        {
            Fragments = fragments;
            DepthLimit = depthLimit;
            NodeLimit = nodeLimit;
            AliasLimit = aliasLimit;
            visitedFragments = null;
            nodeCount = 0;
            aliasCount = 0;
        }

        public void CountNode()
        {
            nodeCount++;
            if (NodeLimit is int n && nodeCount > n)
                Fail($"Query exceeds maximum allowed node count of {n}");
        }

        public void CountAlias()
        {
            aliasCount++;
            if (AliasLimit is int a && aliasCount > a)
                Fail($"Query exceeds maximum allowed alias count of {a}");
        }

        /// <summary>
        /// Prevents an infinite walk across self-referential fragments. Fragment cycles are also
        /// caught by the parser (GraphQLParser.ValidateFragmentCycles) but we guard here defensively
        /// so validation is a single, cheap, non-throwing walk on pathological inputs.
        /// </summary>
        public bool MarkVisitedFragment(string name)
        {
            visitedFragments ??= new HashSet<string>(System.StringComparer.Ordinal);
            return visitedFragments.Add(name);
        }

        public static void Fail(string message) => throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, message);
    }
}
