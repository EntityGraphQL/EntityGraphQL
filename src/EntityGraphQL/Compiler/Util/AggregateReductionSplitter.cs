using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.Util;

/// <summary>
/// Recognises a collection reduction whose per-element selector uses a service —
/// e.g. <c>db.Movies.Sum(m => svc.Score(m.Id))</c> — and splits it into two halves so it can run
/// across the two-pass execution:
///   1. a DB-translatable deps projection (<c>db.Movies.Select(m => new { Id = m.Id })</c>) run in pass 1, and
///   2. the same reduction rebuilt over the materialized deps (<c>list.Sum(x => svc.Score(x.Id))</c>) run in pass 2.
///
/// This is the aggregate analogue of <see cref="EntityGraphQL.Schema.FieldExtensions.FilterSplitter"/>.
/// </summary>
public sealed class AggregateReductionSplit
{
    // Reductions over a collection that take an element selector. Count/Any/All are excluded (no per-element service selector).
    private static readonly HashSet<string> ReductionMethods = ["Sum", "Average", "Min", "Max"];

    public Expression Source { get; }
    /// <summary>The reduction - Sum, Average, Min or Max</summary>
    public string Method { get; }

    /// <summary>The per-element value - for an async reduction, the element's task</summary>
    public LambdaExpression Selector { get; }
    public bool IsQueryableSource { get; }

    /// <summary>True for an async element field's AsyncAggregates.Reduce - the element values are awaited</summary>
    public bool IsAsync => asyncReduce != null;

    // the aggregate function over the awaited values (values => values.Sum(...)) of an async reduction
    private readonly LambdaExpression? asyncReduce;
    private readonly Type elementType;
    private readonly Type depsType;

    // member accesses on the selector's element parameter, keyed by the deps-type field name
    private readonly Dictionary<string, MemberExpression> elementDeps;

    private AggregateReductionSplit(
        Expression source,
        string method,
        LambdaExpression selector,
        bool isQueryableSource,
        Type elementType,
        Type depsType,
        Dictionary<string, MemberExpression> elementDeps,
        LambdaExpression? asyncReduce
    )
    {
        this.asyncReduce = asyncReduce;
        Source = source;
        Method = method;
        Selector = selector;
        IsQueryableSource = isQueryableSource;
        this.elementType = elementType;
        this.depsType = depsType;
        this.elementDeps = elementDeps;
    }

    /// <summary>
    /// True if <paramref name="body"/> is a reduction over a collection whose selector references one of
    /// <paramref name="serviceParams"/>, and the source collection itself does not use a service.
    /// </summary>
    [Obsolete("Pass the schema, whose types the split's are built with: TryCreate(schema, body, serviceParams, out split)")]
    public static bool TryCreate(Expression body, IReadOnlyList<ParameterExpression> serviceParams, out AggregateReductionSplit? split) =>
        TryCreate(LinqRuntimeTypeBuilder.Shared, body, serviceParams, out split);

    /// <summary>
    /// True if <paramref name="body"/> is a reduction over a collection whose selector references one of
    /// <paramref name="serviceParams"/>, and the source collection itself does not use a service.
    /// </summary>
    public static bool TryCreate(ISchemaProvider schema, Expression body, IReadOnlyList<ParameterExpression> serviceParams, out AggregateReductionSplit? split) =>
        TryCreate(LinqRuntimeTypeBuilder.For(schema), body, serviceParams, out split);

    private static bool TryCreate(LinqRuntimeTypeBuilder types, Expression body, IReadOnlyList<ParameterExpression> serviceParams, out AggregateReductionSplit? split)
    {
        split = null;
        if (serviceParams.Count == 0 || body is not MethodCallExpression call)
            return false;
        // an async element field: AsyncAggregates.Reduce(source, selector, values => values.Sum(...), context)
        var asyncReduce = call.Method.DeclaringType == typeof(AsyncAggregates) && call.Arguments.Count == 4 ? call.Arguments[2] as LambdaExpression : null;
        var method = asyncReduce?.Body is MethodCallExpression reduceCall ? reduceCall.Method.Name : call.Method.Name;
        if (!ReductionMethods.Contains(method) || (asyncReduce == null && call.Arguments.Count != 2))
            return false;

        var source = call.Arguments[0];
        var selectorArg = call.Arguments[1];
        if (selectorArg is UnaryExpression quote && quote.NodeType == ExpressionType.Quote)
            selectorArg = quote.Operand;
        if (selectorArg is not LambdaExpression selector || selector.Parameters.Count != 1)
            return false;

        // the service must NOT be used in the source (source must run on the DB)
        if (UsesAny(source, serviceParams) || !TryCollectDeps(selector.Body, selector.Parameters[0], serviceParams, out var collector))
            return false;

        var elementParam = selector.Parameters[0];
        var elementType = elementParam.Type;

        var deps = new Dictionary<string, MemberExpression>();
        var depFieldTypes = new Dictionary<string, System.Type>();
        foreach (var member in collector.Members)
        {
            var name = member.Member.Name;
            if (deps.ContainsKey(name))
                continue;
            deps[name] = member;
            depFieldTypes[name] = member.Type;
        }

        var depsType = types.GetDynamicType(depFieldTypes, "aggDeps");
        var isQueryable = source.Type.IsGenericTypeQueryable();
        split = new AggregateReductionSplit(source, method, selector, isQueryable, elementType, depsType, deps, asyncReduce);
        return true;
    }

    /// <summary>
    /// True if a field resolving <paramref name="resolve"/> can be aggregated across the two passes: it uses one of
    /// <paramref name="serviceParams"/>, and reads the element only through its members (the values pass 1 fetches)
    /// </summary>
    public static bool CanSplit(Expression resolve, ParameterExpression elementParam, IReadOnlyList<ParameterExpression> serviceParams) =>
        TryCollectDeps(resolve, elementParam, serviceParams, out _);

    private static bool TryCollectDeps(Expression selectorBody, ParameterExpression elementParam, IReadOnlyList<ParameterExpression> serviceParams, out ElementMemberCollector collector)
    {
        // collect the member accesses on the element parameter that the selector needs (e.g. m.Id). The element
        // itself (svc.For(m)) is not fetched in pass 1, so a selector using it can not be split
        collector = new ElementMemberCollector(elementParam);
        collector.Visit(selectorBody);
        return serviceParams.Count > 0 && UsesAny(selectorBody, serviceParams) && collector.Members.Count > 0 && !collector.UsesElement;
    }

    /// <summary>
    /// Pass 1: <c>source.Select(m => new DepsType { ... })</c> — a DB-translatable projection of just the element
    /// values the service needs. Returns IQueryable/IEnumerable of the deps type.
    /// </summary>
    public Expression BuildDepsProjection()
    {
        var param = Expression.Parameter(elementType, "m");
        // each deps field is named after the element member it captures (m.Id -> field "Id"), so bind field <- param.<name>
        var bindings = depsType.GetFields().Select(f => (MemberBinding)Expression.Bind(f, Expression.MakeMemberAccess(param, elementDeps[f.Name].Member)));
        var projector = Expression.Lambda(Expression.MemberInit(Expression.New(depsType), bindings), param);
        return Expression.Call(IsQueryableSource ? typeof(Queryable) : typeof(Enumerable), nameof(Enumerable.Select), [elementType, depsType], Source, IsQueryableSource ? Expression.Quote(projector) : projector);
    }

    /// <summary>
    /// Pass 2: rebuild the reduction over the materialized deps list, remapping each element member access
    /// (m.Id) onto the deps element (x.Id) and keeping the service call intact.
    /// </summary>
    public Expression BuildReduce(Expression materializedDeps) => BuildReduce(materializedDeps, null);

    /// <summary>
    /// Pass 2, as BuildReduce(materializedDeps). An async reduction's elements are awaited under
    /// <paramref name="asyncContext"/> (an AsyncAggregateContext - the request's concurrency limits and cancellation)
    /// </summary>
    internal Expression BuildReduce(Expression materializedDeps, Expression? asyncContext)
    {
        var depsElementType = materializedDeps.Type.GetEnumerableOrArrayType()!;
        var newParam = Expression.Parameter(depsElementType, "x");
        var remapped = new ElementMemberRemapper(Selector.Parameters[0], newParam).Visit(Selector.Body);
        var newSelector = Expression.Lambda(remapped, newParam);
        if (asyncReduce != null)
            return AsyncAggregates.CallReduce(materializedDeps, newSelector, asyncReduce, asyncContext ?? AsyncAggregates.NoContext);
        var resultType = remapped.Type;
        var isQueryable = materializedDeps.Type.IsGenericTypeQueryable();
        var methodClass = isQueryable ? typeof(Queryable) : typeof(Enumerable);
        // Min/Max are generic on <TSource, TResult>; Sum/Average resolve by selector return type
        return Method is "Min" or "Max"
            ? Expression.Call(methodClass, Method, [depsElementType, resultType], materializedDeps, isQueryable ? Expression.Quote(newSelector) : newSelector)
            : Expression.Call(methodClass, Method, [depsElementType], materializedDeps, isQueryable ? Expression.Quote(newSelector) : newSelector);
    }

    private static bool UsesAny(Expression expression, IReadOnlyList<ParameterExpression> parameters)
    {
        var finder = new ParameterUsageFinder(parameters);
        finder.Visit(expression);
        return finder.Found;
    }

    private sealed class ElementMemberCollector(ParameterExpression elementParam) : ExpressionVisitor
    {
        public List<MemberExpression> Members { get; } = [];

        /// <summary>The element is used other than to read a member</summary>
        public bool UsesElement { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == elementParam)
                UsesElement = true;
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression == elementParam)
            {
                Members.Add(node);
                return node;
            }
            return base.VisitMember(node);
        }
    }

    private sealed class ElementMemberRemapper(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression == from)
                return Expression.PropertyOrField(to, node.Member.Name);
            return base.VisitMember(node);
        }
    }

    private sealed class ParameterUsageFinder(IReadOnlyList<ParameterExpression> parameters) : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (parameters.Contains(node))
                Found = true;
            return node;
        }
    }
}
