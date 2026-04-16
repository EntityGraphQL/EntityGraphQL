using System;
using System.Collections.Generic;
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
        BulkResolvers.Add(new CompiledBulkFieldResolver(name, dataSelection, fieldExpression, extractedFields, listExpressionPath, serviceType, isAsync, maxConcurrency));
    }

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
