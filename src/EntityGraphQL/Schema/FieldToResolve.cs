using System;
using System.Linq.Expressions;

namespace EntityGraphQL.Schema;
public class FieldToResolveWithArgs<TContext, TParams> : Field
{
    public FieldToResolveWithArgs(ISchemaProvider schema, string name, string? description, TParams argTypes) : base(schema, name, null, description, argTypes, SchemaBuilder.MakeGraphQlType(schema, typeof(object), null), null)
    {
    }

    public Field Resolve(Expression<Func<TContext, TParams, object>> fieldExpression)
    {
        ProcessResolveExpression(fieldExpression);
        // Because we use the return type as object to make the compile time interface nicer we need to get the real return type
        var returnType = fieldExpression.Body.Type;
        if (fieldExpression.Body.NodeType == ExpressionType.Convert)
            returnType = ((UnaryExpression)fieldExpression.Body).Operand.Type;
        ReturnType = SchemaBuilder.MakeGraphQlType(Schema, returnType, null);
        return this;
    }
    public Field ResolveWithService<TService>(Expression<Func<TContext, TParams, TService, object>> fieldExpression)
    {
        ProcessResolveExpression(fieldExpression);
        // Because we use the return type as object to make the compile time interface nicer we need to get the real return type
        var returnType = fieldExpression.Body.Type;
        if (fieldExpression.Body.NodeType == ExpressionType.Convert)
            returnType = ((UnaryExpression)fieldExpression.Body).Operand.Type;
        ReturnType = SchemaBuilder.MakeGraphQlType(Schema, returnType, null);
        Services = new[] { typeof(TService) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2>(Expression<Func<TContext, TParams, TService1, TService2, object>> fieldExpression)
    {
        ProcessResolveExpression(fieldExpression);
        // Because we use the return type as object to make the compile time interface nicer we need to get the real return type
        var returnType = fieldExpression.Body.Type;
        if (fieldExpression.Body.NodeType == ExpressionType.Convert)
            returnType = ((UnaryExpression)fieldExpression.Body).Operand.Type;
        ReturnType = SchemaBuilder.MakeGraphQlType(Schema, returnType, null);
        Services = new[] { typeof(TService1), typeof(TService2) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3>(Expression<Func<TContext, TParams, TService1, TService2, TService3, object>> fieldExpression)
    {
        ProcessResolveExpression(fieldExpression);
        // Because we use the return type as object to make the compile time interface nicer we need to get the real return type
        var returnType = fieldExpression.Body.Type;
        if (fieldExpression.Body.NodeType == ExpressionType.Convert)
            returnType = ((UnaryExpression)fieldExpression.Body).Operand.Type;
        ReturnType = SchemaBuilder.MakeGraphQlType(Schema, returnType, null);
        Services = new[] { typeof(TService1), typeof(TService2), typeof(TService3) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3, TService4>(Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, object>> fieldExpression)
    {
        ProcessResolveExpression(fieldExpression);
        // Because we use the return type as object to make the compile time interface nicer we need to get the real return type
        var returnType = fieldExpression.Body.Type;
        if (fieldExpression.Body.NodeType == ExpressionType.Convert)
            returnType = ((UnaryExpression)fieldExpression.Body).Operand.Type;
        ReturnType = SchemaBuilder.MakeGraphQlType(Schema, returnType, null);
        Services = new[] { typeof(TService1), typeof(TService2), typeof(TService3), typeof(TService4) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3, TService4, TService5>(Expression<Func<TContext, TParams, TService1, TService2, TService3, TService4, TService5, object>> fieldExpression)
    {
        ProcessResolveExpression(fieldExpression);
        // Because we use the return type as object to make the compile time interface nicer we need to get the real return type
        var returnType = fieldExpression.Body.Type;
        if (fieldExpression.Body.NodeType == ExpressionType.Convert)
            returnType = ((UnaryExpression)fieldExpression.Body).Operand.Type;
        ReturnType = SchemaBuilder.MakeGraphQlType(Schema, returnType, null);
        Services = new[] { typeof(TService1), typeof(TService2), typeof(TService3), typeof(TService4), typeof(TService5) };
        return this;
    }
}

public class FieldToResolve<TContext> : Field
{
    public FieldToResolve(ISchemaProvider schema, string name, string? description, object? argTypes) : base(schema, name, null, description, argTypes, SchemaBuilder.MakeGraphQlType(schema, typeof(object), null), null)
    {
    }

    public Field Resolve(Expression<Func<TContext, object>> fieldExpression)
    {
        ProcessResolveExpression(fieldExpression);
        // Because we use the return type as object to make the compile time interface nicer we need to get the real return type
        var returnType = fieldExpression.Body.Type;
        if (fieldExpression.Body.NodeType == ExpressionType.Convert)
            returnType = ((UnaryExpression)fieldExpression.Body).Operand.Type;
        ReturnType = SchemaBuilder.MakeGraphQlType(Schema, returnType, null);
        return this;
    }
    public Field ResolveWithService<TService>(Expression<Func<TContext, TService, object>> fieldExpression)
    {
        ProcessResolveExpression(fieldExpression);
        // Because we use the return type as object to make the compile time interface nicer we need to get the real return type
        var returnType = fieldExpression.Body.Type;
        if (fieldExpression.Body.NodeType == ExpressionType.Convert)
            returnType = ((UnaryExpression)fieldExpression.Body).Operand.Type;
        ReturnType = SchemaBuilder.MakeGraphQlType(Schema, returnType, null);
        Services = new[] { typeof(TService) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2>(Expression<Func<TContext, TService1, TService2, object>> fieldExpression)
    {
        ProcessResolveExpression(fieldExpression);
        // Because we use the return type as object to make the compile time interface nicer we need to get the real return type
        var returnType = fieldExpression.Body.Type;
        if (fieldExpression.Body.NodeType == ExpressionType.Convert)
            returnType = ((UnaryExpression)fieldExpression.Body).Operand.Type;
        ReturnType = SchemaBuilder.MakeGraphQlType(Schema, returnType, null);
        Services = new[] { typeof(TService1), typeof(TService2) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3>(Expression<Func<TContext, TService1, TService2, TService3, object>> fieldExpression)
    {
        ProcessResolveExpression(fieldExpression);
        // Because we use the return type as object to make the compile time interface nicer we need to get the real return type
        var returnType = fieldExpression.Body.Type;
        if (fieldExpression.Body.NodeType == ExpressionType.Convert)
            returnType = ((UnaryExpression)fieldExpression.Body).Operand.Type;
        ReturnType = SchemaBuilder.MakeGraphQlType(Schema, returnType, null);
        Services = new[] { typeof(TService1), typeof(TService2), typeof(TService3) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3, TService4>(Expression<Func<TContext, TService1, TService2, TService3, TService4, object>> fieldExpression)
    {
        ProcessResolveExpression(fieldExpression);
        // Because we use the return type as object to make the compile time interface nicer we need to get the real return type
        var returnType = fieldExpression.Body.Type;
        if (fieldExpression.Body.NodeType == ExpressionType.Convert)
            returnType = ((UnaryExpression)fieldExpression.Body).Operand.Type;
        ReturnType = SchemaBuilder.MakeGraphQlType(Schema, returnType, null);
        Services = new[] { typeof(TService1), typeof(TService2), typeof(TService3), typeof(TService4) };
        return this;
    }
    public Field ResolveWithServices<TService1, TService2, TService3, TService4, TService5>(Expression<Func<TContext, TService1, TService2, TService3, TService4, TService5, object>> fieldExpression)
    {
        ProcessResolveExpression(fieldExpression);
        // Because we use the return type as object to make the compile time interface nicer we need to get the real return type
        var returnType = fieldExpression.Body.Type;
        if (fieldExpression.Body.NodeType == ExpressionType.Convert)
            returnType = ((UnaryExpression)fieldExpression.Body).Operand.Type;
        ReturnType = SchemaBuilder.MakeGraphQlType(Schema, returnType, null);
        Services = new[] { typeof(TService1), typeof(TService2), typeof(TService3), typeof(TService4), typeof(TService5) };
        return this;
    }
}