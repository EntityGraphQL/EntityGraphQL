using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
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
    public bool IsAsync => false;
    public int? MaxConcurrency => null;

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
    public bool IsAsync => false;
    public int? MaxConcurrency => null;

    public BulkFieldResolverWithArgs(
        string name,
        Expression<Func<IEnumerable<TKey>, TParams, TService, IDictionary<TKey, TResult>>> fieldExpression,
        Expression<Func<TContext, TKey>> dataSelector,
        IEnumerable<GraphQLExtractedField> extractedFields
    )
    {
        this.fieldExpression = fieldExpression;
        this.dataSelector = dataSelector;
        ExtractedFields = extractedFields;
        Name = name;
    }
}

public class AsyncBulkFieldResolver<TContext, TService, TKey, TResult> : IBulkFieldResolver
{
    private readonly Expression<Func<IEnumerable<TKey>, TService, Task<IDictionary<TKey, TResult>>>> fieldExpression;
    private readonly Expression<Func<TContext, TKey>> dataSelector;

    public LambdaExpression FieldExpression => fieldExpression;
    public LambdaExpression DataSelector => dataSelector;

    public IEnumerable<GraphQLExtractedField> ExtractedFields { get; }
    public string Name { get; }

    public ParameterExpression? BulkArgParam => null;
    public bool IsAsync => true;
    public int? MaxConcurrency { get; }

    public AsyncBulkFieldResolver(
        string name,
        Expression<Func<IEnumerable<TKey>, TService, Task<IDictionary<TKey, TResult>>>> fieldExpression,
        Expression<Func<TContext, TKey>> dataSelector,
        IEnumerable<Compiler.GraphQLExtractedField> extractedFields,
        int? maxConcurrency = null
    )
    {
        this.fieldExpression = fieldExpression;
        this.dataSelector = dataSelector;
        ExtractedFields = extractedFields;
        Name = name;
        MaxConcurrency = maxConcurrency;
    }
}

public class AsyncBulkFieldResolverWithArgs<TContext, TParams, TService, TKey, TResult> : IBulkFieldResolver
{
    private readonly Expression<Func<IEnumerable<TKey>, TParams, TService, Task<IDictionary<TKey, TResult>>>> fieldExpression;
    private readonly Expression<Func<TContext, TKey>> dataSelector;

    public LambdaExpression FieldExpression => fieldExpression;
    public LambdaExpression DataSelector => dataSelector;

    public IEnumerable<GraphQLExtractedField> ExtractedFields { get; }
    public string Name { get; }
    public ParameterExpression? BulkArgParam => fieldExpression.Parameters[1];
    public bool IsAsync => true;
    public int? MaxConcurrency { get; }

    public AsyncBulkFieldResolverWithArgs(
        string name,
        Expression<Func<IEnumerable<TKey>, TParams, TService, Task<IDictionary<TKey, TResult>>>> fieldExpression,
        Expression<Func<TContext, TKey>> dataSelector,
        IEnumerable<GraphQLExtractedField> extractedFields,
        int? maxConcurrency = null
    )
    {
        this.fieldExpression = fieldExpression;
        this.dataSelector = dataSelector;
        ExtractedFields = extractedFields;
        Name = name;
        MaxConcurrency = maxConcurrency;
    }
}

/// <summary>
/// A bulk resolver taking two services - e.g. the loader's own service plus an engine-supplied
/// <see cref="IFieldSelection"/> so it can fetch only what will be read.
/// </summary>
public class BulkFieldResolver<TContext, TService1, TService2, TKey, TResult> : IBulkFieldResolver
{
    private readonly Expression<Func<IEnumerable<TKey>, TService1, TService2, IDictionary<TKey, TResult>>> fieldExpression;
    private readonly Expression<Func<TContext, TKey>> dataSelector;

    public LambdaExpression FieldExpression => fieldExpression;
    public LambdaExpression DataSelector => dataSelector;

    public IEnumerable<GraphQLExtractedField> ExtractedFields { get; }
    public string Name { get; }

    public ParameterExpression? BulkArgParam => null;
    public bool IsAsync => false;
    public int? MaxConcurrency => null;

    public BulkFieldResolver(
        string name,
        Expression<Func<IEnumerable<TKey>, TService1, TService2, IDictionary<TKey, TResult>>> fieldExpression,
        Expression<Func<TContext, TKey>> dataSelector,
        IEnumerable<GraphQLExtractedField> extractedFields
    )
    {
        this.fieldExpression = fieldExpression;
        this.dataSelector = dataSelector;
        ExtractedFields = extractedFields;
        Name = name;
    }
}

/// <summary>Async form of <see cref="BulkFieldResolver{TContext, TService1, TService2, TKey, TResult}"/>.</summary>
public class AsyncBulkFieldResolver<TContext, TService1, TService2, TKey, TResult> : IBulkFieldResolver
{
    private readonly Expression<Func<IEnumerable<TKey>, TService1, TService2, Task<IDictionary<TKey, TResult>>>> fieldExpression;
    private readonly Expression<Func<TContext, TKey>> dataSelector;

    public LambdaExpression FieldExpression => fieldExpression;
    public LambdaExpression DataSelector => dataSelector;

    public IEnumerable<GraphQLExtractedField> ExtractedFields { get; }
    public string Name { get; }

    public ParameterExpression? BulkArgParam => null;
    public bool IsAsync => true;
    public int? MaxConcurrency { get; }

    public AsyncBulkFieldResolver(
        string name,
        Expression<Func<IEnumerable<TKey>, TService1, TService2, Task<IDictionary<TKey, TResult>>>> fieldExpression,
        Expression<Func<TContext, TKey>> dataSelector,
        IEnumerable<GraphQLExtractedField> extractedFields,
        int? maxConcurrency = null
    )
    {
        this.fieldExpression = fieldExpression;
        this.dataSelector = dataSelector;
        ExtractedFields = extractedFields;
        Name = name;
        MaxConcurrency = maxConcurrency;
    }
}

public interface IBulkFieldResolver
{
    LambdaExpression FieldExpression { get; }
    LambdaExpression DataSelector { get; }
    IEnumerable<GraphQLExtractedField> ExtractedFields { get; }
    string Name { get; }
    ParameterExpression? BulkArgParam { get; }
    bool IsAsync { get; }
    int? MaxConcurrency { get; }
}
