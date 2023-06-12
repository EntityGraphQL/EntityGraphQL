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
        SetUpField(fieldExpression, true, true);
        return this;
    }
    public Field ResolveWithService<TService>(Expression<Func<TContext, TParams, TService, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, true);
        Services = new[] { fieldExpression.Parameters[2] };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2>(Expression<Func<TContext, TParams, TService1, TService2, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, true);
        Services = new[] { fieldExpression.Parameters[2], fieldExpression.Parameters[3] };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3>(Expression<Func<TContext, TParams, TService1, TService2, TService3, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, true);
        Services = new[] { fieldExpression.Parameters[2], fieldExpression.Parameters[3], fieldExpression.Parameters[4] };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3, TService4>(Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, true);
        Services = new[] { fieldExpression.Parameters[2], fieldExpression.Parameters[3], fieldExpression.Parameters[4], fieldExpression.Parameters[5] };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3, TService4, TService5>(Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, TService5, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, true);
        Services = new[] { fieldExpression.Parameters[2], fieldExpression.Parameters[3], fieldExpression.Parameters[4], fieldExpression.Parameters[5], fieldExpression.Parameters[6] };
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
        SetUpField(fieldExpression, false, false);
        return this;
    }

    public Field ResolveWithService<TService>(Expression<Func<TContext, TService, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, false);
        Services = new[] { fieldExpression.Parameters[1] };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2>(Expression<Func<TContext, TService1, TService2, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, false);
        Services = new[] { fieldExpression.Parameters[1], fieldExpression.Parameters[2] };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3>(Expression<Func<TContext, TService1, TService2, TService3, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, false);
        Services = new[] { fieldExpression.Parameters[1], fieldExpression.Parameters[2], fieldExpression.Parameters[3] };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3, TService4>(Expression<Func<TContext, TService1, TService2, TService3, TService4, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, false);
        Services = new[] { fieldExpression.Parameters[1], fieldExpression.Parameters[2], fieldExpression.Parameters[3], fieldExpression.Parameters[4] };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3, TService4, TService5>(Expression<Func<TContext, TService1, TService2, TService3, TService4, TService5, object>> fieldExpression)
    {
        SetUpField(fieldExpression, true, false);
        Services = new[] { fieldExpression.Parameters[1], fieldExpression.Parameters[2], fieldExpression.Parameters[3], fieldExpression.Parameters[4], fieldExpression.Parameters[5] };
        return this;
    }
}