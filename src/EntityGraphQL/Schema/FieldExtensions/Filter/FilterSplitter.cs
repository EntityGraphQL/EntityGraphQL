using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Schema.FieldExtensions;

public sealed class FilterSplitter
{
    private readonly Type listType;

    public FilterSplitter(Type listType)
    {
        this.listType = listType;
    }

    public FilterSplitResult SplitFilter(LambdaExpression filterExpression)
    {
        var splitVisitor = new ExpressionSplitVisitor();
        splitVisitor.Visit(filterExpression.Body);

        // If there are no service markers, treat whole filter as non-service
        if (!splitVisitor.ContainsServiceMarker)
            return new FilterSplitResult(filterExpression, null);

        LambdaExpression? nonServiceFilter = null;
        LambdaExpression? serviceFilter = null;

        if (splitVisitor.NonServiceParts?.Count > 0)
        {
            var nonServiceBody = splitVisitor.NonServiceParts.Aggregate(Expression.AndAlso);
            nonServiceFilter = Expression.Lambda(typeof(Func<,>).MakeGenericType(listType, typeof(bool)), nonServiceBody, filterExpression.Parameters[0]);
        }

        if (splitVisitor.ServiceParts?.Count > 0)
        {
            var serviceBody = splitVisitor.ServiceParts.Aggregate(Expression.AndAlso);
            serviceFilter = Expression.Lambda(typeof(Func<,>).MakeGenericType(listType, typeof(bool)), serviceBody, filterExpression.Parameters[0]);
        }

        return new FilterSplitResult(nonServiceFilter, serviceFilter);
    }
}

// Internal helpers for splitting filter expressions
public sealed class FilterSplitResult
{
    public LambdaExpression? NonServiceFilter { get; }
    public LambdaExpression? ServiceFilter { get; }

    public FilterSplitResult(LambdaExpression? nonServiceFilter, LambdaExpression? serviceFilter)
    {
        NonServiceFilter = nonServiceFilter;
        ServiceFilter = serviceFilter;
    }
}

public sealed class ServiceMarkerCheckVisitor : ExpressionVisitor
{
    public bool ContainsServiceMarker { get; private set; }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(ServiceExpressionMarker))
        {
            ContainsServiceMarker = true;
            return node;
        }
        return ContainsServiceMarker ? node : base.VisitMethodCall(node);
    }

    public override Expression? Visit(Expression? node)
    {
        // Early termination if we already found a service marker
        return ContainsServiceMarker ? node : base.Visit(node);
    }
}

internal sealed class ExpressionSplitVisitor : ExpressionVisitor
{
    private readonly List<Expression> nonServiceParts = [];
    private readonly List<Expression> serviceParts = [];

    public List<Expression>? NonServiceParts => nonServiceParts;
    public List<Expression>? ServiceParts => serviceParts;
    public bool ContainsServiceMarker { get; private set; }

    public override Expression? Visit(Expression? node)
    {
        if (node == null)
            return null;

        if (node is BinaryExpression binary)
        {
            if (binary.NodeType == ExpressionType.AndAlso)
            {
                Visit(binary.Left);
                Visit(binary.Right);
                return node;
            }
            if (binary.NodeType == ExpressionType.OrElse)
            {
                if (ContainsServiceField(node))
                {
                    serviceParts.Add(node);
                    ContainsServiceMarker = true;
                }
                else
                    nonServiceParts.Add(node);
                return node;
            }
        }

        if (node.NodeType == ExpressionType.Not)
        {
            if (ContainsServiceField(node))
            {
                serviceParts.Add(node);
                ContainsServiceMarker = true;
            }
            else
                nonServiceParts.Add(node);
            return node;
        }

        if (ContainsServiceField(node))
        {
            serviceParts.Add(node);
            ContainsServiceMarker = true;
        }
        else
            nonServiceParts.Add(node);

        return node;
    }

    private bool ContainsServiceField(Expression expression)
    {
        var visitor = new ServiceMarkerCheckVisitor();
        visitor.Visit(expression);
        if (visitor.ContainsServiceMarker)
            ContainsServiceMarker = true;
        return visitor.ContainsServiceMarker;
    }
}
