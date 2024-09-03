using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

/// <summary>
/// Class to hold required services and constant parameters required to execute the compiled query
/// </summary>
public class CompileContext
{
    private readonly List<ParameterExpression> servicesCollected = new();
    private readonly Dictionary<ParameterExpression, object?> constantParameters = new();
    private readonly Dictionary<IField, ParameterExpression> constantParametersForField = new();

    public CompileContext(ExecutionOptions options, Dictionary<string, object>? bulkData, QueryRequestContext requestContext)
    {
        BulkData = bulkData;
        BulkParameter = bulkData != null ? Expression.Parameter(bulkData.GetType(), "bulkData") : null;
        ExecutionOptions = options;
        RequestContext = requestContext;
    }

    public List<ParameterExpression> Services
    {
        get => servicesCollected;
    }
    public IReadOnlyDictionary<ParameterExpression, object?> ConstantParameters
    {
        get => constantParameters;
    }
    public List<CompiledBulkFieldResolver> BulkResolvers { get; private set; } = new();
    public Dictionary<string, object>? BulkData { get; }
    public ParameterExpression? BulkParameter { get; }
    public ExecutionOptions ExecutionOptions { get; }
    public QueryRequestContext RequestContext { get; }

    public void AddServices(IEnumerable<ParameterExpression> services)
    {
        foreach (var service in services)
        {
            servicesCollected.Add(service);
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

    public void AddBulkResolver(string name, LambdaExpression dataSelection, LambdaExpression fieldExpression, Expression listExpression, IEnumerable<GraphQLExtractedField> extractedFields)
    {
        BulkResolvers.Add(new CompiledBulkFieldResolver(name, dataSelection, fieldExpression, listExpression, extractedFields));
    }

    public void AddArgsToCompileContext(
        IField field,
        IReadOnlyDictionary<string, object> args,
        ParameterExpression? docParam,
        object? docVariables,
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

public class CompiledBulkFieldResolver
{
    public string Name { get; private set; }
    public LambdaExpression DataSelection { get; private set; }
    public LambdaExpression FieldExpression { get; private set; }
    public Expression ListExpression { get; }
    public IEnumerable<GraphQLExtractedField> ExtractedFields { get; }

    public CompiledBulkFieldResolver(string name, LambdaExpression dataSelection, LambdaExpression fieldExpression, Expression listExpression, IEnumerable<GraphQLExtractedField> extractedFields)
    {
        this.Name = name;
        this.DataSelection = dataSelection;
        this.FieldExpression = fieldExpression;
        ListExpression = listExpression;
        ExtractedFields = extractedFields;
    }
}
