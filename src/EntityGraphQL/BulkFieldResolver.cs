using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Schema;

public class BulkFieldResolver<TContext, TService, TKey, TResult> : IBulkFieldResolver
{
    private readonly Expression<Func<IEnumerable<TKey>, TService, IDictionary<TKey, TResult>>> fieldExpression;
    private readonly Expression<Func<TContext, TKey>> dataSelector;

    public LambdaExpression FieldExpression => fieldExpression;
    public LambdaExpression DataSelector => dataSelector;

    public IEnumerable<GraphQLExtractedField> ExtractedFields { get; }
    public string Name { get; }

    public ParameterExpression? BulkArgParam => null;

    public BulkFieldResolver(
        string name,
        Expression<Func<IEnumerable<TKey>, TService, IDictionary<TKey, TResult>>> fieldExpression,
        Expression<Func<TContext, TKey>> dataSelector,
        IEnumerable<Compiler.GraphQLExtractedField> extractedFields
    )
    {
        this.fieldExpression = fieldExpression;
        this.dataSelector = dataSelector;
        ExtractedFields = extractedFields;
        Name = name;
    }
}

public class BulkFieldResolverWithArgs<TContext, TParams, TService, TKey, TResult> : IBulkFieldResolver
{
    private readonly Expression<Func<IEnumerable<TKey>, TParams, TService, IDictionary<TKey, TResult>>> fieldExpression;
    private readonly Expression<Func<TContext, TKey>> dataSelector;

    public LambdaExpression FieldExpression => fieldExpression;
    public LambdaExpression DataSelector => dataSelector;

    public IEnumerable<GraphQLExtractedField> ExtractedFields { get; }
    public string Name { get; }
    public ParameterExpression? BulkArgParam => fieldExpression.Parameters[1];

    public BulkFieldResolverWithArgs(
        string name,
        Expression<Func<IEnumerable<TKey>, TParams, TService, IDictionary<TKey, TResult>>> fieldExpression,
        Expression<Func<TContext, TKey>> dataSelector,
        IEnumerable<Compiler.GraphQLExtractedField> extractedFields
    )
    {
        this.fieldExpression = fieldExpression;
        this.dataSelector = dataSelector;
        ExtractedFields = extractedFields;
        Name = name;
    }
}

public interface IBulkFieldResolver
{
    LambdaExpression FieldExpression { get; }
    LambdaExpression DataSelector { get; }
    IEnumerable<GraphQLExtractedField> ExtractedFields { get; }
    string Name { get; }
    ParameterExpression? BulkArgParam { get; }
}
