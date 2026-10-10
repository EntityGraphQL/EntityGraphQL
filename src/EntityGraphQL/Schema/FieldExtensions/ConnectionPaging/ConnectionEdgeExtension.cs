using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions;

public class ConnectionEdgeExtension : BaseFieldExtension
{
    private readonly Type listType;
    private readonly ParameterExpression firstSelectParam;

    /// <param name="listType">The element type of the paged collection</param>
    public ConnectionEdgeExtension(Type listType)
    {
        this.listType = listType;
        firstSelectParam = Expression.Parameter(listType, "edgeNode");
    }

    /// <param name="listType">The element type of the paged collection</param>
    /// <param name="isQueryable">Not used. The edges field is shared by every field paging a collection of listType, root
    /// (IQueryable) or nested (e.g. a navigation property), so this is decided per use from the expression</param>
    [Obsolete("isQueryable is not used, it is decided per use from the expression. Use ConnectionEdgeExtension(Type listType). This constructor will be removed in 7.0")]
    public ConnectionEdgeExtension(Type listType, bool isQueryable)
        : this(listType)
    {
        _ = isQueryable;
    }

    public override (Expression? expression, ParameterExpression? originalArgParam, ParameterExpression? newArgParam, object? argumentValue) GetExpressionAndArguments(
        IField field,
        FieldExtensionExpressionContext context
    )
    {
        var fieldNode = context.FieldNode;
        var expression = context.Expression;
        var argumentParam = context.ArgumentParameter;
        var arguments = context.Arguments;
        var fieldContext = context.Context;
        var servicesPass = context.ServicesPass;
        var withoutServiceFields = context.WithoutServiceFields;
        var parameterReplacer = context.ParameterReplacer;
        var originalArgParam = context.OriginalArgumentParameter;
        var compileContext = context.CompileContext;

        // We know we need the arguments from the parent field as that is where they are defined
        if (fieldNode.ParentNode != null)
        {
            argumentParam =
                compileContext.GetConstantParameterForField(fieldNode.ParentNode.Field!)
                ?? throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, $"Could not find arguments for field '{fieldNode.ParentNode.Field!.Name}' in compile context.");
            arguments = compileContext.ConstantParameters[argumentParam];
            originalArgParam = fieldNode.ParentNode.Field!.ArgumentsParameter;
        }

        if (argumentParam == null)
            throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, "ConnectionEdgeExtension requires an argument parameter to be passed in");
        // field.Resolve will be built with the original field context and needs to be updated
        // we use the resolveExpression & extensions from our parent extension. We need to figure this out at runtime as the type this Edges field
        // is on may be used in multiple places and have different arguments etc
        // See OffsetConnectionPagingTests.TestMultiUseWithArgs
        var pagingExtension = (ConnectionPagingExtension)fieldNode.ParentNode!.Field!.Extensions.Find(e => e is ConnectionPagingExtension)!;
        var parentField = fieldNode.ParentNode!.Field!;

        // For fields WITHOUT services in second pass: skip (paging done in first pass).
        // For service-backed paging fields the parent (pagedConfigs) returns early from GetFieldExpression
        // in the first pass, so edges is never reached then — paging is always built in the second pass.
        if (servicesPass && parentField.Services.Count == 0)
            return (expression, originalArgParam, argumentParam, arguments);

        // Build the paging expression using the original field expression.
        // This happens in first pass for non-service fields, or second pass for service fields.
        // In the services pass (second pass), the grandparent list's element type has changed to an
        // anonymous type. We look up the replacement context stored by GraphQLListSelectionField and
        // use ExpressionReplacer to correctly remap member accesses (e.g. dir.Id → anonElem.id).
        var grandparentContext = fieldNode.ParentNode!.ParentNode!.NextFieldContext;
        if (servicesPass && parentField.Services.Count > 0 && grandparentContext is ParameterExpression grandparentParam)
        {
            var replacement = compileContext.GetFieldContextReplacement(grandparentParam);
            if (replacement != null && parentField.ExtractedFieldsFromServices != null)
            {
                var expReplacer = new ExpressionReplacer(parentField.ExtractedFieldsFromServices, replacement, false, false, null);
                expression = expReplacer.Replace(pagingExtension.OriginalFieldExpression!);
                if (parentField.FieldParam != null)
                    expression = parameterReplacer.Replace(expression, parentField.FieldParam, replacement);
            }
            else if (replacement != null)
            {
                expression = parameterReplacer.Replace(pagingExtension.OriginalFieldExpression!, parentField.FieldParam!, replacement);
            }
            else
            {
                expression = parameterReplacer.Replace(pagingExtension.OriginalFieldExpression!, parentField.FieldParam!, grandparentContext!);
            }
        }
        else
        {
            expression = parameterReplacer.Replace(pagingExtension.OriginalFieldExpression!, parentField.FieldParam!, grandparentContext!);
        }

        // expression here is the adjusted Connection<T>(). This field (edges) is where we deal with the list again - field.Resolve
        foreach (var extension in pagingExtension.ExtensionsBeforePaging)
        {
            var res = extension.GetExpressionAndArguments(
                field,
                new FieldExtensionExpressionContext
                {
                    FieldNode = fieldNode,
                    Expression = expression,
                    ArgumentParameter = argumentParam,
                    Arguments = arguments,
                    Context = fieldContext,
                    ServicesPass = servicesPass,
                    WithoutServiceFields = withoutServiceFields,
                    ParameterReplacer = parameterReplacer,
                    OriginalArgumentParameter = originalArgParam,
                    CompileContext = compileContext,
                }
            );
            (expression, originalArgParam, argumentParam, arguments) = (res.Item1!, res.Item2, res.Item3!, res.Item4);
        }

        arguments ??= new { };
        var isQueryable = expression.Type.IsGenericTypeQueryable();

        // check and set up arguments
        if (arguments.Before != null && arguments.After != null)
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Field '{fieldNode.ParentNode.Name}' - Field only supports either before or after being supplied, not both.");
        if (arguments.First != null && arguments.First < 0)
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Field '{fieldNode.ParentNode.Name}' - first argument can not be less than 0.");
        if (arguments.Last != null && arguments.Last < 0)
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Field '{fieldNode.ParentNode.Name}' - last argument can not be less than 0.");

        // deserialize cursors here once (not many times in the fields)
        arguments.AfterNum = ConnectionHelper.DeserializeCursor(arguments.After);
        arguments.BeforeNum = ConnectionHelper.DeserializeCursor(arguments.Before);

        if (pagingExtension.MaxPageSize.HasValue)
        {
            if (arguments.First != null && arguments.First > pagingExtension.MaxPageSize.Value)
                throw new EntityGraphQLException(
                    GraphQLErrorCategory.DocumentError,
                    $"Field '{fieldNode.ParentNode.Name}' - first argument can not be greater than {pagingExtension.MaxPageSize.Value}."
                );
            if (arguments.Last != null && arguments.Last > pagingExtension.MaxPageSize.Value)
                throw new EntityGraphQLException(
                    GraphQLErrorCategory.DocumentError,
                    $"Field '{fieldNode.ParentNode.Name}' - last argument can not be greater than {pagingExtension.MaxPageSize.Value}."
                );
        }

        if (arguments.First == null && arguments.Last == null && pagingExtension.DefaultPageSize != null)
            arguments.First = pagingExtension.DefaultPageSize;

        // A collection on a parent object (e.g. a navigation property) is part of the parent's projection, so a LINQ
        // provider like EF has to translate it. A root field's collection is executed by itself, as is the services pass in memory
        var nestedProjection = !isQueryable && !servicesPass && fieldNode.ParentNode.IsRootField != true;
        // arguments.TotalCount is this collection's count only for a root field - a nested collection's parents share the arguments
        var limitBefore = Expression.Constant(fieldNode.ParentNode.IsRootField == true);
        Expression skipExp = Expression.Call(typeof(ConnectionHelper), nameof(ConnectionHelper.GetSkipNumber), null, argumentParam, Expression.Constant(true), limitBefore);
        Expression takeExp = Expression.Call(
            typeof(ConnectionHelper),
            nameof(ConnectionHelper.GetTakeNumber),
            null,
            argumentParam,
            Expression.Call(typeof(ConnectionHelper), nameof(ConnectionHelper.GetSkipNumber), null, argumentParam, Expression.Constant(false), limitBefore)
        );
        Expression? edgeExpression;
        if (!nestedProjection)
        {
            edgeExpression = Expression.Call(
                isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions),
                nameof(EnumerableExtensions.Take),
                [listType],
                Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), nameof(EnumerableExtensions.Skip), [listType], expression, skipExp),
                takeExp
            );
        }
        else
        {
            // EF knows System.Linq's Skip/Take but not our int? overloads
            if (arguments.Last != null && arguments.BeforeNum == null)
            {
                // The last N of each parent's own collection. GetSkipNumber/GetTakeNumber use arguments.TotalCount, which
                // is one value per request so only works for a root field. Skip(Count() - N) has the outer row in a
                // correlated subquery EF can't translate on every provider, so take the first N in reverse order and
                // flip them back once materialized (ProcessExpressionPostSelection). `after` is applied there too, as
                // Skip(after).Reverse() needs SQL APPLY (not supported by SQLite) - it can only drop rows from this page.
                // Reverse() needs an ordered collection for EF. An unordered in-memory collection works, so we can't reject it here
                edgeExpression = Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.Take),
                    [listType],
                    Expression.Call(typeof(Enumerable), nameof(Enumerable.Reverse), [listType], expression),
                    Expression.Constant((int)arguments.Last)
                );
            }
            else
            {
                // last + before: the page ending at before, which is wrong for a parent whose collection does not reach the
                // cursor - ProcessExpressionPostSelection adds that parent's tail. GetLastBeforeSkip marks this page for it
                if (arguments.Last != null)
                    skipExp = Expression.Call(typeof(ConnectionHelper), nameof(ConnectionHelper.GetLastBeforeSkip), null, argumentParam);
                skipExp = Expression.Coalesce(skipExp, Expression.Constant(0));
                // No take means the rest of the collection: int.MaxValue - skip rather than int.MaxValue, as EF pages a nested
                // collection with ROW_NUMBER() and `row <= skip + take`, which overflows an int on SQL Server/Postgres
                takeExp = Expression.Coalesce(takeExp, Expression.Subtract(Expression.Constant(int.MaxValue), skipExp));
                edgeExpression = Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.Take),
                    [listType],
                    Expression.Call(typeof(Enumerable), nameof(Enumerable.Skip), [listType], expression, skipExp),
                    takeExp
                );
            }
        }

        // we have moved the expression from the parent node to here. We need to call the before callback
        if (fieldNode.ParentNode?.IsRootField == true)
            BaseGraphQLField.HandleBeforeRootFieldExpressionBuild(
                compileContext,
                BaseGraphQLField.GetOperationName((BaseGraphQLField)fieldNode.ParentNode),
                fieldNode.ParentNode.Name!,
                servicesPass,
                fieldNode.ParentNode.IsRootField,
                ref edgeExpression
            );

        // First we select the edge node as the full object
        // we later change this to a anonymous object to not have the full table returned from EF
        // This happens later as we don't know what the query has selected yet
        expression = Expression.Call(
            isQueryable ? typeof(Queryable) : typeof(Enumerable),
            nameof(Enumerable.Select),
            [listType, field.ReturnType.SchemaType.TypeDotnet],
            edgeExpression,
            // we have the node selection from ConnectionEdgeNodeExtension we can insert into here for a nice EF compatible query
            Expression.Lambda(
                Expression.MemberInit(
                    Expression.New(field.ReturnType.SchemaType.TypeDotnet),
                    new List<MemberBinding> { Expression.Bind(field.ReturnType.SchemaType.TypeDotnet.GetProperty("Node")!, firstSelectParam) }
                ),
                firstSelectParam
            )
        );

        return (expression, originalArgParam, argumentParam, arguments);
    }

    public override (Expression baseExpression, Dictionary<IFieldKey, CompiledField> selectionExpressions, ParameterExpression? selectContextParam) ProcessExpressionSelection(
        FieldExtensionSelectionContext context
    )
    {
        var baseExpression = context.BaseExpression;
        var selectionExpressions = context.SelectionExpressions;
        var selectContextParam = context.SelectContextParameter;
        var argumentParam = context.ArgumentParameter;
        var servicesPass = context.ServicesPass;
        var parameterReplacer = context.ParameterReplacer;

        if (argumentParam == null)
            throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, "ConnectionEdgeExtension requires an argument parameter to be passed in");

        if (servicesPass)
            return (baseExpression, selectionExpressions, selectContextParam);

        // we now know the fields they want to select so we rebuild the base expression
        // remove the above Select(new ConnectionEdge<T>(), ...)
        baseExpression = ((MethodCallExpression)baseExpression).Arguments[0];
        var isQueryable = baseExpression.Type.IsGenericTypeQueryable();
        // remove null check as it is not required
        var nodeField = selectionExpressions.First(f => f.Key.SchemaName == "node").Value;
        var anonNewExpression = nodeField.Expression;
        if (nodeField.Expression.NodeType == ExpressionType.Conditional)
            anonNewExpression = ((ConditionalExpression)anonNewExpression).IfFalse;
        var anonType = anonNewExpression.Type;
        var edgeType = typeof(ConnectionEdge<>).MakeGenericType(anonType);
        var edgeParam = Expression.Parameter(edgeType, "newEdgeParam");
        var newNodeExpression = parameterReplacer.Replace(anonNewExpression, selectContextParam!, firstSelectParam);

        baseExpression = Expression.Call(
            isQueryable ? typeof(Queryable) : typeof(Enumerable),
            nameof(Enumerable.Select),
            [listType, edgeType],
            baseExpression,
            // we have the node selection from ConnectionEdgeNodeExtension we can insert into here for a nice EF compatible query
            Expression.Lambda(Expression.MemberInit(Expression.New(edgeType), new List<MemberBinding> { Expression.Bind(edgeType.GetProperty("Node")!, newNodeExpression) }), firstSelectParam)
        );

        nodeField.Expression = Expression.PropertyOrField(edgeParam, "Node");
        if (FindPagingCall(baseExpression) == null)
        {
            // assign cursors while enumerating (in memory - this is what executes the DB query above).
            // ApplyCursors computes the argument-dependent cursor base once per request rather than per row.
            baseExpression = Expression.Call(
                typeof(ConnectionHelper),
                nameof(ConnectionHelper.ApplyCursors),
                [anonType],
                baseExpression,
                argumentParam,
                Expression.Constant(LimitsBefore(baseExpression))
            );
            foreach (var cursorField in selectionExpressions.Where(f => f.Key.SchemaName == "cursor"))
                cursorField.Value.Expression = Expression.PropertyOrField(edgeParam, "Cursor");
        }
        else
        {
            // A nested collection is part of the parent's projection and the provider (e.g. EF) can not translate
            // ApplyCursors there. Select no cursor here and set it once the edges are a list - see ProcessExpressionPostSelection
            foreach (var cursorField in selectionExpressions.Where(f => f.Key.SchemaName == "cursor"))
                cursorField.Value.Expression = Expression.Constant(null, typeof(string));
        }

        return (baseExpression, selectionExpressions, edgeParam);
    }

    public override Expression ProcessExpressionPostSelection(FieldExtensionPostSelectionContext context)
    {
        var resultExpression = context.ResultExpression;
        if (context.ServicesPass || context.ArgumentParameter == null)
            return resultExpression;
        var elementType = resultExpression.Type.GetEnumerableOrArrayType();
        if (elementType == null || !typeof(List<>).MakeGenericType(elementType).IsAssignableFrom(resultExpression.Type))
            return resultExpression;
        var paging = FindPaging(resultExpression);
        if (paging == null)
            return resultExpression;
        var (takeCall, pagingCall) = paging.Value;

        // only the nested case left the cursors to us - see ProcessExpressionSelection
        var cursorFields = string.Join(",", context.SelectionExpressions.Where(f => f.Key.SchemaName == "cursor" && f.Value.Expression is ConstantExpression { Value: null }).Select(f => f.Key.Name));
        var collection = pagingCall.Arguments[0];
        var argumentParam = context.ArgumentParameter;

        // last + before: also select each parent's tail (its last items, in reverse) and its count, and pick per parent
        // once materialized - see ConnectionHelper.PickLastBeforePage. Done even without cursors as it picks the rows
        if (pagingCall.Method.Name == nameof(Enumerable.Skip) && CallsMethod(pagingCall.Arguments[1], nameof(ConnectionHelper.GetLastBeforeSkip)))
        {
            var last = Expression.Convert(Expression.PropertyOrField(argumentParam, "Last"), typeof(int));
            var tailTake = Expression.Call(typeof(Enumerable), nameof(Enumerable.Take), [listType], Expression.Call(typeof(Enumerable), nameof(Enumerable.Reverse), [listType], collection), last);
            var tail = new ReplaceNode(takeCall, tailTake).Visit(resultExpression)!;
            return Expression.Call(
                typeof(ConnectionHelper),
                nameof(ConnectionHelper.PickLastBeforePage),
                [elementType],
                resultExpression,
                tail,
                Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), [listType], collection),
                Expression.Convert(Expression.PropertyOrField(argumentParam, "BeforeNum"), typeof(int)),
                last,
                Expression.Constant(cursorFields)
            );
        }

        if (cursorFields.Length == 0)
            return resultExpression;

        // The edges of a page are rows skip + 1, skip + 2, ... of the collection. Use the paging built in GetExpressionAndArguments
        // as it is per parent for `last`. EF translates it alongside the page and calls SetCursors with the materialized list
        Expression skip;
        Expression reversedFromCount;
        if (pagingCall.Method.Name == nameof(Enumerable.Reverse))
        {
            // the last of each parent's own Count() rows, taken in reverse. SetCursors drops the rows up to `after`
            skip = Expression.Call(typeof(ConnectionHelper), nameof(ConnectionHelper.GetAfterNumber), null, argumentParam);
            reversedFromCount = Expression.Convert(Expression.Call(typeof(Enumerable), nameof(Enumerable.Count), [listType], collection), typeof(int?));
        }
        else
        {
            skip = pagingCall.Arguments[1];
            reversedFromCount = Expression.Constant(null, typeof(int?));
        }

        // field names as a string - EF rejects non-primitive constants passed to a method in a client projection
        return Expression.Call(typeof(ConnectionHelper), nameof(ConnectionHelper.SetCursors), [elementType], resultExpression, skip, reversedFromCount, Expression.Constant(cursorFields));
    }

    /// <summary>
    /// Finds the Take() built for a nested collection in GetExpressionAndArguments and the Skip() (or Reverse() for last)
    /// under it. Null if the paging was not built for a nested collection
    /// </summary>
    private (MethodCallExpression take, MethodCallExpression paging)? FindPaging(Expression expression)
    {
        while (expression is MethodCallExpression call)
        {
            // the paging Take() is the outermost one on the collection
            if (call.Method.Name == nameof(Enumerable.Take) && call.Method.IsGenericMethod && call.Method.GetGenericArguments()[0] == listType)
            {
                if (
                    call.Method.DeclaringType == typeof(Enumerable)
                    && call.Arguments[0] is MethodCallExpression { Method.Name: nameof(Enumerable.Skip) or nameof(Enumerable.Reverse) } paging
                    && paging.Method.DeclaringType == typeof(Enumerable)
                )
                    return (call, paging);
                return null;
            }
            expression = call.Arguments[0];
        }
        return null;
    }

    private MethodCallExpression? FindPagingCall(Expression expression) => FindPaging(expression)?.paging;

    /// <summary>Whether the page was built limiting a before cursor to the collection's count (a root field)</summary>
    private static bool LimitsBefore(Expression expression) =>
        FindCall(expression, call => call.Method.Name == nameof(ConnectionHelper.GetSkipNumber) && call.Arguments.Count == 3 && call.Arguments[2] is ConstantExpression { Value: true }) != null;

    private static bool CallsMethod(Expression expression, string name) => FindCall(expression, call => call.Method.DeclaringType == typeof(ConnectionHelper) && call.Method.Name == name) != null;

    private static MethodCallExpression? FindCall(Expression expression, Func<MethodCallExpression, bool> match)
    {
        var finder = new CallFinder(match);
        finder.Visit(expression);
        return finder.Found;
    }

    private sealed class CallFinder(Func<MethodCallExpression, bool> match) : ExpressionVisitor
    {
        public MethodCallExpression? Found { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (Found == null && match(node))
                Found = node;
            return base.VisitMethodCall(node);
        }
    }

    private sealed class ReplaceNode(Expression from, Expression to) : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node) => node == from ? to : base.Visit(node);
    }
}
