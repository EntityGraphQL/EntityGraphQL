using System;
using System.Linq.Expressions;

namespace EntityGraphQL.Schema;
public class FieldToResolveWithArgs<TContext, TParams> : Field
{
    public FieldToResolveWithArgs(ISchemaProvider schema, ISchemaType fromType, string name, string? description, TParams argTypes) : base(schema, fromType, name, null, description, argTypes, SchemaBuilder.MakeGraphQlType(schema, typeof(object), null), null)
    {
    }

    public Field Resolve(Expression<Func<TContext, TParams, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true);
        return this;
    }
    public Field ResolveWithService<TService>(Expression<Func<TContext, TParams, TService, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true);
        Services = new[] { typeof(TService) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2>(Expression<Func<TContext, TParams, TService1, TService2, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true);
        Services = new[] { typeof(TService1), typeof(TService2) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3>(Expression<Func<TContext, TParams, TService1, TService2, TService3, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true);
        Services = new[] { typeof(TService1), typeof(TService2), typeof(TService3) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3, TService4>(Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true);
        Services = new[] { typeof(TService1), typeof(TService2), typeof(TService3), typeof(TService4) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3, TService4, TService5>(Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, TService5, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true);
        Services = new[] { typeof(TService1), typeof(TService2), typeof(TService3), typeof(TService4), typeof(TService5) };
        return this;
    }
}

public class FieldToResolve<TContext> : Field
{
    public FieldToResolve(ISchemaProvider schema, ISchemaType fromType, string name, string? description, object? argTypes) : base(schema, fromType, name, null, description, argTypes, SchemaBuilder.MakeGraphQlType(schema, typeof(object), null), null)
    {
    }

    public Field Resolve(Expression<Func<TContext, object>> fieldExpression)
    {
        SetUpField(fieldExpression, false);
        return this;
    }

    public Field ResolveWithService<TService>(Expression<Func<TContext, TService, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true);
        Services = new[] { typeof(TService) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2>(Expression<Func<TContext, TService1, TService2, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true);
        Services = new[] { typeof(TService1), typeof(TService2) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3>(Expression<Func<TContext, TService1, TService2, TService3, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true);
        Services = new[] { typeof(TService1), typeof(TService2), typeof(TService3) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3, TService4>(Expression<Func<TContext, TService1, TService2, TService3, TService4, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true);
        Services = new[] { typeof(TService1), typeof(TService2), typeof(TService3), typeof(TService4) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3, TService4, TService5>(Expression<Func<TContext, TService1, TService2, TService3, TService4, TService5, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true);
        Services = new[] { typeof(TService1), typeof(TService2), typeof(TService3), typeof(TService4), typeof(TService5) };
        return this;
    }
}