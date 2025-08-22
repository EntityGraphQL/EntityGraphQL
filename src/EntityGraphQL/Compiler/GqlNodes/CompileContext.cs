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

    public CompileContext(ExecutionOptions options, Dictionary<string, object>? bulkData, QueryRequestContext requestContext, CancellationToken cancellationToken = default)
    {
        BulkData = bulkData;
        BulkParameter = bulkData != null ? Expression.Parameter(bulkData.GetType(), "bulkData") : null;
        ExecutionOptions = options;
        RequestContext = requestContext;
        CancellationToken = cancellationToken;
    }

    public List<ParameterExpression> Services { get; } = [];
    public IReadOnlyDictionary<ParameterExpression, object?> ConstantParameters => constantParameters;
    public List<CompiledBulkFieldResolver> BulkResolvers { get; private set; } = [];
    public Dictionary<string, object>? BulkData { get; }
    public ParameterExpression? BulkParameter { get; }
    public ExecutionOptions ExecutionOptions { get; }
    public QueryRequestContext RequestContext { get; }
    public CancellationToken CancellationToken { get; }
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
        List<string> validationErrors,
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
