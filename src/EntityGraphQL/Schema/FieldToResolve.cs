using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Schema;

/// <summary>
/// Represents a field with arguments that has a resolve expression set. The generics allow compile time checking of the bulk resolver.
/// </summary>
/// <typeparam name="TContext"></typeparam>
/// <typeparam name="TParams"></typeparam>
public class FieldWithContextAndArgs<TContext, TParams> : Field
{
    public FieldWithContextAndArgs(ISchemaProvider schema, ISchemaType fromType, string name, string? description, TParams argTypes)
        : base(schema, fromType, name, null, description, argTypes, SchemaBuilder.MakeGraphQlType(schema, fromType.GqlType == GqlTypes.InputObject, typeof(object), null, name, fromType), null) { }

    public Field ResolveBulk<TService, TKey, TResult>(Expression<Func<TContext, TKey>> dataSelector, Expression<Func<IEnumerable<TKey>, TParams, TService, IDictionary<TKey, TResult>>> fieldExpression)
    {
        var extractor = new ExpressionExtractor();
        var keyParam = dataSelector.Parameters.First();
        var fields = extractor.Extract(dataSelector, keyParam, false)?.Select(i => new GraphQLExtractedField(Schema, i.Key, i.Value, keyParam))!;
        ExtractedFieldsFromServices!.AddRange(fields);
        BulkResolver = new BulkFieldResolverWithArgs<TContext, TParams, TService, TKey, TResult>($"bulk_{FromType.Name}.{Name}", fieldExpression, dataSelector, fields);
        Services.Add(fieldExpression.Parameters[2]);
        return this;
    }

    /// <summary>
    /// Add async bulk resolver for this field with optional concurrency limiting
    /// </summary>
    /// <param name="dataSelector">Expression to select keys for bulk loading</param>
    /// <param name="fieldExpression">Async expression to resolve bulk data</param>
    /// <param name="maxConcurrency">Maximum number of concurrent bulk operations</param>
    /// <returns>The field with async bulk resolver configured</returns>
    public Field ResolveBulkAsync<TService, TKey, TResult>(
        Expression<Func<TContext, TKey>> dataSelector,
        Expression<Func<IEnumerable<TKey>, TParams, TService, Task<IDictionary<TKey, TResult>>>> fieldExpression,
        int? maxConcurrency = null
    )
    {
        var extractor = new ExpressionExtractor();
        var keyParam = dataSelector.Parameters.First();
        var fields = extractor.Extract(dataSelector, keyParam, false)?.Select(i => new GraphQLExtractedField(Schema, i.Key, i.Value, keyParam))!;
        ExtractedFieldsFromServices!.AddRange(fields);
        BulkResolver = new AsyncBulkFieldResolverWithArgs<TContext, TParams, TService, TKey, TResult>($"bulk_{FromType.Name}.{Name}", fieldExpression, dataSelector, fields, maxConcurrency);
        Services.Add(fieldExpression.Parameters[2]);
        return this;
    }
}

/// <summary>
/// Represents a field that has a resolve expression set. The generics allow compile time checking of the bulk resolver.
/// </summary>
/// <typeparam name="TContext"></typeparam>
public class FieldWithContext<TContext> : Field
{
    public FieldWithContext(ISchemaProvider schema, ISchemaType fromType, string name, string? description, object? argTypes)
        : base(schema, fromType, name, null, description, argTypes, SchemaBuilder.MakeGraphQlType(schema, fromType.GqlType == GqlTypes.InputObject, typeof(object), null, name, fromType), null) { }

    public Field ResolveBulk<TService, TKey, TResult>(Expression<Func<TContext, TKey>> dataSelector, Expression<Func<IEnumerable<TKey>, TService, IDictionary<TKey, TResult>>> fieldExpression)
    {
        var extractor = new ExpressionExtractor();
        var keyParam = dataSelector.Parameters.First();
        var fields = extractor.Extract(dataSelector, keyParam, false)?.Select(i => new GraphQLExtractedField(Schema, i.Key, i.Value, keyParam))!;
        ExtractedFieldsFromServices!.AddRange(fields);
        BulkResolver = new BulkFieldResolver<TContext, TService, TKey, TResult>($"bulk_{FromType.Name}.{Name}", fieldExpression, dataSelector, fields);
        Services.Add(fieldExpression.Parameters[1]);
        return this;
    }

    /// <summary>
    /// Add async bulk resolver for this field with optional concurrency limiting
    /// </summary>
    /// <param name="dataSelector">Expression to select keys for bulk loading</param>
    /// <param name="fieldExpression">Async expression to resolve bulk data</param>
    /// <param name="maxConcurrency">Maximum number of concurrent bulk operations</param>
    /// <returns>The field with async bulk resolver configured</returns>
    public Field ResolveBulkAsync<TService, TKey, TResult>(
        Expression<Func<TContext, TKey>> dataSelector,
        Expression<Func<IEnumerable<TKey>, TService, Task<IDictionary<TKey, TResult>>>> fieldExpression,
        int? maxConcurrency = null
    )
    {
        var extractor = new ExpressionExtractor();
        var keyParam = dataSelector.Parameters.First();
        var fields = extractor.Extract(dataSelector, keyParam, false)?.Select(i => new GraphQLExtractedField(Schema, i.Key, i.Value, keyParam))!;
        ExtractedFieldsFromServices!.AddRange(fields);
        BulkResolver = new AsyncBulkFieldResolver<TContext, TService, TKey, TResult>($"bulk_{FromType.Name}.{Name}", fieldExpression, dataSelector, fields, maxConcurrency);
        Services.Add(fieldExpression.Parameters[1]);
        return this;
    }
}

/// <summary>
/// Represents a field with arguments that still needs it's resolve expression to be set. The generics allow compile time checking of the expression.
/// </summary>
/// <typeparam name="TContext"></typeparam>
/// <typeparam name="TParams"></typeparam>
public class FieldToResolveWithArgs<TContext, TParams> : FieldWithContextAndArgs<TContext, TParams>
{
    public FieldToResolveWithArgs(ISchemaProvider schema, ISchemaType fromType, string name, string? description, TParams argTypes)
        : base(schema, fromType, name, description, argTypes) { }

    public Field Resolve(Expression<Func<TContext, TParams, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, true, false);
        return this;
    }

    public FieldWithContextAndArgs<TContext, TParams> Resolve<TService>(Expression<Func<TContext, TParams, TService, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, true, false);
        Services = [fieldExpression.Parameters[2]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContextAndArgs<TContext, TParams> ResolveWithService<TService>(Expression<Func<TContext, TParams, TService, object>> fieldExpression) => Resolve(fieldExpression);

    public FieldWithContextAndArgs<TContext, TParams> Resolve<TService1, TService2>(Expression<Func<TContext, TParams, TService1, TService2, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, true, false);
        Services = [fieldExpression.Parameters[2], fieldExpression.Parameters[3]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContextAndArgs<TContext, TParams> ResolveWithServices<TService1, TService2>(Expression<Func<TContext, TParams, TService1, TService2, object>> fieldExpression) =>
        Resolve(fieldExpression);

    public FieldWithContextAndArgs<TContext, TParams> Resolve<TService1, TService2, TService3>(Expression<Func<TContext, TParams, TService1, TService2, TService3, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, true, false);
        Services = [fieldExpression.Parameters[2], fieldExpression.Parameters[3], fieldExpression.Parameters[4]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContextAndArgs<TContext, TParams> ResolveWithServices<TService1, TService2, TService3>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, object>> fieldExpression
    ) => Resolve(fieldExpression);

    public FieldWithContextAndArgs<TContext, TParams> Resolve<TService1, TService2, TService3, TService4>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, object>> fieldExpression
    )
    {
        SetUpField(fieldExpression, true, true, false);
        Services = [fieldExpression.Parameters[2], fieldExpression.Parameters[3], fieldExpression.Parameters[4], fieldExpression.Parameters[5]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContextAndArgs<TContext, TParams> ResolveWithServices<TService1, TService2, TService3, TService4>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, object>> fieldExpression
    ) => Resolve(fieldExpression);

    public FieldWithContextAndArgs<TContext, TParams> Resolve<TService1, TService2, TService3, TService4, TService5>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, TService5, object>> fieldExpression
    )
    {
        SetUpField(fieldExpression, true, true, false);
        Services = [fieldExpression.Parameters[2], fieldExpression.Parameters[3], fieldExpression.Parameters[4], fieldExpression.Parameters[5], fieldExpression.Parameters[6]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContextAndArgs<TContext, TParams> ResolveWithServices<TService1, TService2, TService3, TService4, TService5>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, TService5, object>> fieldExpression
    ) => Resolve(fieldExpression);

    public FieldWithContextAndArgs<TContext, TParams> ResolveAsync<TService>(Expression<Func<TContext, TParams, TService, Task>> fieldExpression, int? maxConcurrency = null) =>
        ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContextAndArgs<TContext, TParams> ResolveAsync<TService, TReturn>(Expression<Func<TContext, TParams, TService, ValueTask<TReturn>>> fieldExpression, int? maxConcurrency = null) =>
        ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContextAndArgs<TContext, TParams> ResolveAsync<TService, TReturn>(
        Expression<Func<TContext, TParams, TService, IAsyncEnumerable<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContextAndArgs<TContext, TParams> ResolveAsync<TService1, TService2>(Expression<Func<TContext, TParams, TService1, TService2, Task>> fieldExpression, int? maxConcurrency = null) =>
        ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContextAndArgs<TContext, TParams> ResolveAsync<TService1, TService2, TReturn>(
        Expression<Func<TContext, TParams, TService1, TService2, ValueTask<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContextAndArgs<TContext, TParams> ResolveAsync<TService1, TService2, TReturn>(
        Expression<Func<TContext, TParams, TService1, TService2, IAsyncEnumerable<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContextAndArgs<TContext, TParams> ResolveAsync<TService1, TService2, TService3>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, Task>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContextAndArgs<TContext, TParams> ResolveAsync<TService1, TService2, TService3, TReturn>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, ValueTask<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContextAndArgs<TContext, TParams> ResolveAsync<TService1, TService2, TService3, TReturn>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, IAsyncEnumerable<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContextAndArgs<TContext, TParams> ResolveAsync<TService1, TService2, TService3, TService4>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, Task>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContextAndArgs<TContext, TParams> ResolveAsync<TService1, TService2, TService3, TService4, TReturn>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, ValueTask<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContextAndArgs<TContext, TParams> ResolveAsync<TService1, TService2, TService3, TService4, TReturn>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, IAsyncEnumerable<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContextAndArgs<TContext, TParams> ResolveAsync<TService1, TService2, TService3, TService4, TService5>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, TService5, Task>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContextAndArgs<TContext, TParams> ResolveAsync<TService1, TService2, TService3, TService4, TService5, TReturn>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, TService5, ValueTask<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContextAndArgs<TContext, TParams> ResolveAsync<TService1, TService2, TService3, TService4, TService5, TReturn>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, TService5, IAsyncEnumerable<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    private FieldWithContextAndArgs<TContext, TParams> ResolveAsyncImpl(LambdaExpression fieldExpression, int? maxConcurrency)
    {
        SetUpField(fieldExpression, true, false, true);
        Services = [.. fieldExpression.Parameters.Skip(1)];
        var serviceTypes = Services.Select(s => s.Type).ToArray();

        // if return type is Task check that it is Task<> otherwise throw
        if (typeof(Task).IsAssignableFrom(fieldExpression.Body.Type) && !fieldExpression.Body.Type.IsGenericType)
        {
            throw new EntityGraphQLCompilerException("Async field expression must return Task<T> not Task as the field needs a result.");
        }

        // Add concurrency limiting
        Extensions.Add(new ConcurrencyLimitFieldExtension(serviceTypes, maxConcurrency));

        return this;
    }
}

/// <summary>
/// Represents a field that still needs it's resolve expression to be set. The generics allow compile time checking of the expression.
/// </summary>
/// <typeparam name="TContext"></typeparam>
public class FieldToResolve<TContext> : FieldWithContext<TContext>
{
    public FieldToResolve(ISchemaProvider schema, ISchemaType fromType, string name, string? description, object? argTypes)
        : base(schema, fromType, name, description, argTypes) { }

    public Field Resolve(Expression<Func<TContext, object?>> fieldExpression)
    {
        SetUpField(fieldExpression, false, false, false);
        return this;
    }

    public FieldWithContext<TContext> Resolve<TService>(Expression<Func<TContext, TService, object?>> fieldExpression)
    {
        SetUpField(fieldExpression, true, false, false);
        Services = [fieldExpression.Parameters[1]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContext<TContext> ResolveWithService<TService>(Expression<Func<TContext, TService, object?>> fieldExpression) => Resolve(fieldExpression);

    public FieldWithContext<TContext> Resolve<TService1, TService2>(Expression<Func<TContext, TService1, TService2, object?>> fieldExpression)
    {
        SetUpField(fieldExpression, true, false, false);
        Services = [fieldExpression.Parameters[1], fieldExpression.Parameters[2]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContext<TContext> ResolveWithServices<TService1, TService2>(Expression<Func<TContext, TService1, TService2, object?>> fieldExpression) => Resolve(fieldExpression);

    public FieldWithContext<TContext> Resolve<TService1, TService2, TService3>(Expression<Func<TContext, TService1, TService2, TService3, object?>> fieldExpression)
    {
        SetUpField(fieldExpression, true, false, false);
        Services = [fieldExpression.Parameters[1], fieldExpression.Parameters[2], fieldExpression.Parameters[3]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContext<TContext> ResolveWithServices<TService1, TService2, TService3>(Expression<Func<TContext, TService1, TService2, TService3, object?>> fieldExpression) =>
        Resolve(fieldExpression);

    public FieldWithContext<TContext> Resolve<TService1, TService2, TService3, TService4>(Expression<Func<TContext, TService1, TService2, TService3, TService4, object?>> fieldExpression)
    {
        SetUpField(fieldExpression, true, false, false);
        Services = [fieldExpression.Parameters[1], fieldExpression.Parameters[2], fieldExpression.Parameters[3], fieldExpression.Parameters[4]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContext<TContext> ResolveWithServices<TService1, TService2, TService3, TService4>(
        Expression<Func<TContext, TService1, TService2, TService3, TService4, object?>> fieldExpression
    ) => Resolve(fieldExpression);

    public FieldWithContext<TContext> Resolve<TService1, TService2, TService3, TService4, TService5>(
        Expression<Func<TContext, TService1, TService2, TService3, TService4, TService5, object?>> fieldExpression
    )
    {
        SetUpField(fieldExpression, true, false, false);
        Services = [fieldExpression.Parameters[1], fieldExpression.Parameters[2], fieldExpression.Parameters[3], fieldExpression.Parameters[4], fieldExpression.Parameters[5]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContext<TContext> ResolveWithServices<TService1, TService2, TService3, TService4, TService5>(
        Expression<Func<TContext, TService1, TService2, TService3, TService4, TService5, object?>> fieldExpression
    ) => Resolve(fieldExpression);

    /// <summary>
    /// Resolve an async field with optional concurrency limiting
    /// </summary>
    /// <param name="fieldExpression">The async resolver function</param>
    /// <param name="maxConcurrency">Maximum number of concurrent operations for this field</param>
    /// <returns>The resolved field with concurrency limiting applied</returns>
    public FieldWithContext<TContext> ResolveAsync<TService>(Expression<Func<TContext, TService, Task>> fieldExpression, int? maxConcurrency = null) =>
        ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContext<TContext> ResolveAsync<TService, TReturn>(Expression<Func<TContext, TService, ValueTask<TReturn>>> fieldExpression, int? maxConcurrency = null) =>
        ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContext<TContext> ResolveAsync<TService, TReturn>(Expression<Func<TContext, TService, IAsyncEnumerable<TReturn>>> fieldExpression, int? maxConcurrency = null) =>
        ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContext<TContext> ResolveAsync<TService1, TService2>(Expression<Func<TContext, TService1, TService2, Task>> fieldExpression, int? maxConcurrency = null) =>
        ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContext<TContext> ResolveAsync<TService1, TService2, TReturn>(Expression<Func<TContext, TService1, TService2, ValueTask<TReturn>>> fieldExpression, int? maxConcurrency = null) =>
        ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContext<TContext> ResolveAsync<TService1, TService2, TReturn>(
        Expression<Func<TContext, TService1, TService2, IAsyncEnumerable<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContext<TContext> ResolveAsync<TService1, TService2, TService3>(Expression<Func<TContext, TService1, TService2, TService3, Task>> fieldExpression, int? maxConcurrency = null) =>
        ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContext<TContext> ResolveAsync<TService1, TService2, TService3, TReturn>(
        Expression<Func<TContext, TService1, TService2, TService3, ValueTask<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContext<TContext> ResolveAsync<TService1, TService2, TService3, TReturn>(
        Expression<Func<TContext, TService1, TService2, TService3, IAsyncEnumerable<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContext<TContext> ResolveAsync<TService1, TService2, TService3, TService4>(
        Expression<Func<TContext, TService1, TService2, TService3, TService4, Task>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContext<TContext> ResolveAsync<TService1, TService2, TService3, TService4, TReturn>(
        Expression<Func<TContext, TService1, TService2, TService3, TService4, ValueTask<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContext<TContext> ResolveAsync<TService1, TService2, TService3, TService4, TReturn>(
        Expression<Func<TContext, TService1, TService2, TService3, TService4, IAsyncEnumerable<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContext<TContext> ResolveAsync<TService1, TService2, TService3, TService4, TService5>(
        Expression<Func<TContext, TService1, TService2, TService3, TService4, TService5, Task>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContext<TContext> ResolveAsync<TService1, TService2, TService3, TService4, TService5, TReturn>(
        Expression<Func<TContext, TService1, TService2, TService3, TService4, TService5, ValueTask<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    public FieldWithContext<TContext> ResolveAsync<TService1, TService2, TService3, TService4, TService5, TReturn>(
        Expression<Func<TContext, TService1, TService2, TService3, TService4, TService5, IAsyncEnumerable<TReturn>>> fieldExpression,
        int? maxConcurrency = null
    ) => ResolveAsyncImpl(fieldExpression, maxConcurrency);

    private FieldWithContext<TContext> ResolveAsyncImpl(LambdaExpression fieldExpression, int? maxConcurrency)
    {
        SetUpField(fieldExpression, true, false, true);
        Services = [.. fieldExpression.Parameters.Skip(1)];
        var serviceTypes = Services.Select(s => s.Type).ToArray();

        // if return type is Task check that it is Task<> otherwise throw
        if (typeof(Task).IsAssignableFrom(fieldExpression.Body.Type) && !fieldExpression.Body.Type.IsGenericType)
        {
            throw new EntityGraphQLCompilerException("Async field expression must return Task<T> not Task as the field needs a result.");
        }

        // Add concurrency limiting
        Extensions.Add(new ConcurrencyLimitFieldExtension(serviceTypes, maxConcurrency));

        return this;
    }
}
