using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;

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
        BulkResolver = new BulkFieldResolverWithArgs<TContext, TParams, TService, TKey, TResult>($"bulk_{Name}", fieldExpression, dataSelector, fields);
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
        BulkResolver = new BulkFieldResolver<TContext, TService, TKey, TResult>($"bulk_{Name}", fieldExpression, dataSelector, fields);
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
        SetUpField(fieldExpression, true, true);
        return this;
    }

    public FieldWithContextAndArgs<TContext, TParams> Resolve<TService>(Expression<Func<TContext, TParams, TService, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, true);
        Services = [fieldExpression.Parameters[2]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContextAndArgs<TContext, TParams> ResolveWithService<TService>(Expression<Func<TContext, TParams, TService, object>> fieldExpression) => ResolveWithService(fieldExpression);

    public FieldWithContextAndArgs<TContext, TParams> Resolve<TService1, TService2>(Expression<Func<TContext, TParams, TService1, TService2, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, true);
        Services = [fieldExpression.Parameters[2], fieldExpression.Parameters[3]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContextAndArgs<TContext, TParams> ResolveWithServices<TService1, TService2>(Expression<Func<TContext, TParams, TService1, TService2, object>> fieldExpression) =>
        ResolveWithServices(fieldExpression);

    public FieldWithContextAndArgs<TContext, TParams> Resolve<TService1, TService2, TService3>(Expression<Func<TContext, TParams, TService1, TService2, TService3, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, true);
        Services = [fieldExpression.Parameters[2], fieldExpression.Parameters[3], fieldExpression.Parameters[4]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContextAndArgs<TContext, TParams> ResolveWithServices<TService1, TService2, TService3>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, object>> fieldExpression
    ) => ResolveWithServices(fieldExpression);

    public FieldWithContextAndArgs<TContext, TParams> Resolve<TService1, TService2, TService3, TService4>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, object>> fieldExpression
    )
    {
        SetUpField(fieldExpression, true, true);
        Services = [fieldExpression.Parameters[2], fieldExpression.Parameters[3], fieldExpression.Parameters[4], fieldExpression.Parameters[5]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContextAndArgs<TContext, TParams> ResolveWithServices<TService1, TService2, TService3, TService4>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, object>> fieldExpression
    ) => ResolveWithServices(fieldExpression);

    public FieldWithContextAndArgs<TContext, TParams> Resolve<TService1, TService2, TService3, TService4, TService5>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, TService5, object>> fieldExpression
    )
    {
        SetUpField(fieldExpression, true, true);
        Services = [fieldExpression.Parameters[2], fieldExpression.Parameters[3], fieldExpression.Parameters[4], fieldExpression.Parameters[5], fieldExpression.Parameters[6]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContextAndArgs<TContext, TParams> ResolveWithServices<TService1, TService2, TService3, TService4, TService5>(
        Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, TService5, object>> fieldExpression
    ) => ResolveWithServices(fieldExpression);
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
        SetUpField(fieldExpression, false, false);
        return this;
    }

    public FieldWithContext<TContext> Resolve<TService>(Expression<Func<TContext, TService, object?>> fieldExpression)
    {
        SetUpField(fieldExpression, true, false);
        Services = [fieldExpression.Parameters[1]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContext<TContext> ResolveWithService<TService>(Expression<Func<TContext, TService, object?>> fieldExpression) => ResolveWithService(fieldExpression);

    public FieldWithContext<TContext> Resolve<TService1, TService2>(Expression<Func<TContext, TService1, TService2, object?>> fieldExpression)
    {
        SetUpField(fieldExpression, true, false);
        Services = [fieldExpression.Parameters[1], fieldExpression.Parameters[2]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContext<TContext> ResolveWithServices<TService1, TService2>(Expression<Func<TContext, TService1, TService2, object?>> fieldExpression) => ResolveWithServices(fieldExpression);

    public FieldWithContext<TContext> Resolve<TService1, TService2, TService3>(Expression<Func<TContext, TService1, TService2, TService3, object?>> fieldExpression)
    {
        SetUpField(fieldExpression, true, false);
        Services = [fieldExpression.Parameters[1], fieldExpression.Parameters[2], fieldExpression.Parameters[3]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContext<TContext> ResolveWithServices<TService1, TService2, TService3>(Expression<Func<TContext, TService1, TService2, TService3, object?>> fieldExpression) =>
        Resolve(fieldExpression);

    public FieldWithContext<TContext> Resolve<TService1, TService2, TService3, TService4>(Expression<Func<TContext, TService1, TService2, TService3, TService4, object?>> fieldExpression)
    {
        SetUpField(fieldExpression, true, false);
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
        SetUpField(fieldExpression, true, false);
        Services = [fieldExpression.Parameters[1], fieldExpression.Parameters[2], fieldExpression.Parameters[3], fieldExpression.Parameters[4], fieldExpression.Parameters[5]];
        return this;
    }

    [Obsolete("Use Resolve")]
    public FieldWithContext<TContext> ResolveWithServices<TService1, TService2, TService3, TService4, TService5>(
        Expression<Func<TContext, TService1, TService2, TService3, TService4, TService5, object?>> fieldExpression
    ) => Resolve(fieldExpression);
}
