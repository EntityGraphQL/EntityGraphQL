using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Schema.QueryLimits;

/// <summary>
/// Enforces depth, node-count, and alias-count limits on a parsed GraphQL document, and delegates
/// complexity calculation to <see cref="IQueryComplexityAnalyzer"/>. Called pre-execution so exceeded
/// limits abort before any resolver runs.
///
/// In <see cref="QueryLimitsMode.Enforce"/> (the default) validation stops at the first limit hit and throws
/// an <see cref="EntityGraphQLException"/> with <see cref="GraphQLErrorCategory.DocumentError"/>, calling
/// <see cref="ExecutionOptions.OnQueryLimitExceeded"/> first when set. In <see cref="QueryLimitsMode.ReportOnly"/>
/// the full document is walked (so the callback receives the query's actual depth/counts, not just "exceeded"),
/// the callback fires once per exceeded limit and the query executes.
/// </summary>
internal static class QueryLimitsValidator
{
    internal static void Validate(GraphQLDocument document, string? operationName, QueryVariables? variables, ExecutionOptions options)
    {
        var depthLimit = Nz(options.MaxQueryDepth);
        var nodeLimit = Nz(options.MaxFieldSelections);
        var aliasLimit = Nz(options.MaxFieldAliases);
        var complexityLimit = Nz(options.MaxQueryComplexity);

        if (depthLimit is null && nodeLimit is null && aliasLimit is null && complexityLimit is null)
            return;

        var op = string.IsNullOrEmpty(operationName) ? (document.Operations.Count > 0 ? document.Operations[0] : null) : document.Operations.Find(o => o.Name == operationName);
        if (op == null)
            return;

        var reportOnly = options.QueryLimitsMode == QueryLimitsMode.ReportOnly;
        var report = options.OnQueryLimitExceeded;
        var opName = string.IsNullOrEmpty(op.Name) ? null : op.Name;

        if (depthLimit is not null || nodeLimit is not null || aliasLimit is not null)
        {
            var (docParam, docVariables) = DefaultQueryComplexityAnalyzer.BuildDocVariables(op, variables);
            // in report-only mode the walk completes with no limits applied so the totals are the real ones
            var state = new WalkState(document.Fragments, reportOnly ? null : depthLimit, reportOnly ? null : nodeLimit, reportOnly ? null : aliasLimit, docParam, docVariables, report, opName);
            foreach (var field in op.QueryFields)
                Walk(field, 1, ref state);

            if (reportOnly && report != null)
            {
                if (depthLimit is int d && state.MaxDepth > d)
                    report(new QueryLimitExceededContext(QueryLimitKind.QueryDepth, state.MaxDepth, d, opName));
                if (nodeLimit is int n && state.NodeCount > n)
                    report(new QueryLimitExceededContext(QueryLimitKind.FieldSelections, state.NodeCount, n, opName));
                if (aliasLimit is int a && state.AliasCount > a)
                    report(new QueryLimitExceededContext(QueryLimitKind.FieldAliases, state.AliasCount, a, opName));
            }
        }

        if (complexityLimit is int cLimit)
        {
            var analyzer = options.QueryComplexityAnalyzer ?? new DefaultQueryComplexityAnalyzer();
            var cost = analyzer.CalculateComplexity(document, operationName, variables, options);
            if (cost > cLimit)
            {
                report?.Invoke(new QueryLimitExceededContext(QueryLimitKind.QueryComplexity, cost, cLimit, opName));
                if (!reportOnly)
                    throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Query complexity {cost} exceeds maximum allowed complexity {cLimit}");
            }
        }
    }

    private static void Walk(BaseGraphQLField field, int depth, ref WalkState state)
    {
        if (field.IsExcludedByDirectives(state.DocParam, state.DocVariables))
            return;

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
        state.RecordDepth(depth);

        if (state.DepthLimit is int d && depth > d)
            state.Fail(QueryLimitKind.QueryDepth, depth, d, $"Query exceeds maximum allowed depth of {d}");

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
        public readonly ParameterExpression? DocParam;
        public readonly IArgumentsTracker? DocVariables;
        private readonly Action<QueryLimitExceededContext>? report;
        private readonly string? operationName;
        private HashSet<string>? visitedFragments;
        private int nodeCount;
        private int aliasCount;
        private int maxDepth;

        public WalkState(
            IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
            int? depthLimit,
            int? nodeLimit,
            int? aliasLimit,
            ParameterExpression? docParam,
            IArgumentsTracker? docVariables,
            Action<QueryLimitExceededContext>? report,
            string? operationName
        )
        {
            Fragments = fragments;
            DepthLimit = depthLimit;
            NodeLimit = nodeLimit;
            AliasLimit = aliasLimit;
            DocParam = docParam;
            DocVariables = docVariables;
            this.report = report;
            this.operationName = operationName;
            visitedFragments = null;
            nodeCount = 0;
            aliasCount = 0;
            maxDepth = 0;
        }

        public readonly int NodeCount => nodeCount;
        public readonly int AliasCount => aliasCount;
        public readonly int MaxDepth => maxDepth;

        public void CountNode()
        {
            nodeCount++;
            if (NodeLimit is int n && nodeCount > n)
                Fail(QueryLimitKind.FieldSelections, nodeCount, n, $"Query exceeds maximum allowed node count of {n}");
        }

        public void CountAlias()
        {
            aliasCount++;
            if (AliasLimit is int a && aliasCount > a)
                Fail(QueryLimitKind.FieldAliases, aliasCount, a, $"Query exceeds maximum allowed alias count of {a}");
        }

        public void RecordDepth(int depth)
        {
            if (depth > maxDepth)
                maxDepth = depth;
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

        public readonly void Fail(QueryLimitKind kind, int actual, int maximum, string message)
        {
            report?.Invoke(new QueryLimitExceededContext(kind, actual, maximum, operationName));
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, message);
        }
    }
}
