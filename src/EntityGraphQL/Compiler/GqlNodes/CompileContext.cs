using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Compiler;

/// <summary>
/// Class to hold required services and constant parameters required to execute the compiled query
/// </summary>
public class CompileContext
{
    private readonly Dictionary<ParameterExpression, object?> constantParameters = [];
    private readonly Dictionary<IField, ParameterExpression> constantParametersForField = [];
    private readonly Dictionary<ParameterExpression, ParameterExpression> fieldContextReplacements = [];

    public CompileContext(
        ExecutionOptions options,
        Dictionary<string, object>? bulkData,
        QueryRequestContext requestContext,
        ParameterExpression? docParam,
        IArgumentsTracker? docVariables,
        CancellationToken cancellationToken = default
    )
    {
        BulkData = bulkData;
        BulkParameter = bulkData != null ? Expression.Parameter(bulkData.GetType(), "bulkData") : null;
        ExecutionOptions = options;
        RequestContext = requestContext;
        CancellationToken = cancellationToken;
        // Store document variables for access in field extensions and EQL compilation
        DocumentVariablesParameter = docParam;
        DocumentVariables = docVariables;
    }

    public List<ParameterExpression> Services { get; } = [];
    public IReadOnlyDictionary<ParameterExpression, object?> ConstantParameters => constantParameters;

    /// <summary>
    /// Set by <see cref="ExecutableGraphQLStatement"/> when <see cref="ExecutionOptions.CacheCompiledDelegates"/> is true.
    /// Carries the cache instance so <c>ExecuteExpressionAsync</c> can look up / store compiled delegates.
    /// </summary>
    internal QueryCache? DelegateCache { get; set; }

    /// <summary>
    /// Base key for the delegate cache — <c>"{statementId}:{variablesHash}"</c>.
    /// The pass suffix (":1" or ":2") is appended inside <c>ExecuteExpressionAsync</c>.
    /// </summary>
    internal string? DelegateCacheKeyBase { get; set; }
    public List<CompiledBulkFieldResolver> BulkResolvers { get; private set; } = [];
    public Dictionary<string, object>? BulkData { get; }
    public ParameterExpression? BulkParameter { get; }
    public ExecutionOptions ExecutionOptions { get; }
    public QueryRequestContext RequestContext { get; }
    public CancellationToken CancellationToken { get; }
    public ParameterExpression? DocumentVariablesParameter { get; }
    public IArgumentsTracker? DocumentVariables { get; }
    public ConcurrencyLimiterRegistry ConcurrencyLimiterRegistry { get; } = new ConcurrencyLimiterRegistry();

    public void AddServices(IEnumerable<ParameterExpression> services)
    {
        foreach (var service in services)
        {
            Services.Add(service);
        }
    }

    public void AddConstant(IField? fromField, ParameterExpression parameterExpression, object? value)
    {
        constantParameters[parameterExpression] = value;
        if (fromField != null)
            constantParametersForField[fromField] = parameterExpression;
    }

    public ParameterExpression? GetConstantParameterForField(IField field)
    {
        if (constantParametersForField.TryGetValue(field, out var param))
            return param;
        return null;
    }

    /// <summary>
    /// The dynamic types produced for an interface/union list selection during the first-pass compile, read
    /// again when building the second (services) pass. Keyed by the query node and stored here because the
    /// node belongs to the cached document shared across requests - per-request state must not live on it.
    /// </summary>
    private readonly Dictionary<IGraphQLNode, List<Type>> possibleNextContextTypes = [];

    public void SetPossibleNextContextTypes(IGraphQLNode node, List<Type> types) => possibleNextContextTypes[node] = types;

    public List<Type>? GetPossibleNextContextTypes(IGraphQLNode node) => possibleNextContextTypes.TryGetValue(node, out var types) ? types : null;

    /// <summary>
    /// Carry the first pass's state into the context used for the second (services) pass.
    /// </summary>
    internal void CopyPossibleNextContextTypesFrom(CompileContext other)
    {
        foreach (var kvp in other.possibleNextContextTypes)
            possibleNextContextTypes[kvp.Key] = kvp.Value;
    }

    /// <summary>
    /// Stores the second-pass element parameter that replaces an original list element parameter.
    /// Used by paging extensions (ConnectionEdgeExtension, OffsetPagingItemsExtension) to get the
    /// correct anonymous-type element when building service expressions in the second pass.
    /// </summary>
    public void SetFieldContextReplacement(ParameterExpression original, ParameterExpression replacement) => fieldContextReplacements[original] = replacement;

    public ParameterExpression? GetFieldContextReplacement(ParameterExpression original)
    {
        if (fieldContextReplacements.TryGetValue(original, out var p))
            return p;
        return null;
    }

    public void AddBulkResolver(
        string name,
        LambdaExpression dataSelection,
        LambdaExpression fieldExpression,
        IEnumerable<GraphQLExtractedField> extractedFields,
        List<IGraphQLNode> listExpressionPath,
        Type serviceType
    )
    {
        if (!HasBulkResolver(name, listExpressionPath))
            BulkResolvers.Add(new CompiledBulkFieldResolver(name, dataSelection, fieldExpression, extractedFields, listExpressionPath, serviceType));
    }

    public void AddBulkResolver(
        string name,
        LambdaExpression dataSelection,
        LambdaExpression fieldExpression,
        IEnumerable<GraphQLExtractedField> extractedFields,
        List<IGraphQLNode> listExpressionPath,
        Type serviceType,
        bool isAsync,
        int? maxConcurrency
    )
    {
        if (!HasBulkResolver(name, listExpressionPath))
            BulkResolvers.Add(new CompiledBulkFieldResolver(name, dataSelection, fieldExpression, extractedFields, listExpressionPath, serviceType, isAsync, maxConcurrency));
    }

    /// <summary>
    /// The same bulk field can be registered more than once for the same selection point - e.g. selected
    /// directly and again via a fragment spread. Loading it twice is wasted service calls; the loaded data
    /// is keyed by name so the duplicate would just overwrite the first.
    /// </summary>
    private bool HasBulkResolver(string name, List<IGraphQLNode> listExpressionPath) =>
        BulkResolvers.Any(b => b.Name == name && b.ListExpressionPath.SequenceEqual(listExpressionPath));

    /// <summary>
    /// The chain of selection nodes currently being compiled (statement -> root field -> nested fields).
    /// Bulk resolvers use a snapshot of this as their list expression path instead of walking a field's
    /// ParentNode chain - fields selected via a fragment spread carry the fragment STATEMENT as their
    /// parent chain, not the selection point the fragment is used at. Fragment nodes expand rather than
    /// compile a selection, so they never appear here.
    /// </summary>
    private readonly List<IGraphQLNode> selectionPath = [];

    internal void PushSelectionPathNode(IGraphQLNode node) => selectionPath.Add(node);

    internal void PopSelectionPathNode() => selectionPath.RemoveAt(selectionPath.Count - 1);

    internal List<IGraphQLNode> SelectionPathSnapshot() => [.. selectionPath];

    public void AddArgsToCompileContext(
        IField field,
        IReadOnlyDictionary<string, object?> args,
        ParameterExpression? docParam,
        IArgumentsTracker? docVariables,
        ref object? argumentValue,
        HashSet<string> validationErrors,
        ParameterExpression? newArgParam
    )
    {
        if (field.FieldParam != null && field.ArgumentsParameter != null)
        {
            // we need to make a copy of the argument parameter as if they select the same field multiple times
            // i.e. with different alias & arguments we need to have different ParameterExpression instances
            argumentValue = ArgumentUtil.BuildArgumentsObject(field.Schema, field.Name, field, args, field.Arguments.Values, newArgParam?.Type, docParam, docVariables, validationErrors);
            if (argumentValue != null)
                AddConstant(field, newArgParam!, argumentValue);
        }
    }
}
