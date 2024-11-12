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
    private readonly bool isQueryable;

    public ConnectionEdgeExtension(Type listType, bool isQueryable)
    {
        this.listType = listType;
        this.isQueryable = isQueryable;
        firstSelectParam = Expression.Parameter(listType, "edgeNode");
    }

    public override (Expression? expression, ParameterExpression? originalArgParam, ParameterExpression? newArgParam, object? argumentValue) GetExpressionAndArguments(
        IField field,
        Expression expression,
        ParameterExpression? argumentParam,
        dynamic? arguments,
        Expression context,
        IGraphQLNode? parentNode,
        bool servicesPass,
        ParameterReplacer parameterReplacer,
        ParameterExpression? originalArgParam,
        CompileContext compileContext
    )
    {
        // We know we need the arguments from the parent field as that is where they are defined
        if (parentNode != null)
        {
            argumentParam =
                compileContext.GetConstantParameterForField(parentNode.Field!)
                ?? throw new EntityGraphQLCompilerException($"Could not find arguments for field '{parentNode.Field!.Name}' in compile context.");
            arguments = compileContext.ConstantParameters[argumentParam];
            originalArgParam = parentNode.Field!.ArgumentsParameter;
        }

        if (argumentParam == null)
            throw new EntityGraphQLCompilerException("ConnectionEdgeExtension requires an argument parameter to be passed in");
        // field.Resolve will be built with the original field context and needs to be updated
        // we use the resolveExpression & extensions from our parent extension. We need to figure this out at runtime as the type this Edges field
        // is on may be used in multiple places and have different arguments etc
        // See OffsetConnectionPagingTests.TestMultiUseWithArgs
        var pagingExtension = (ConnectionPagingExtension)parentNode!.Field!.Extensions.Find(e => e is ConnectionPagingExtension)!;
        expression = servicesPass ? expression : parameterReplacer.Replace(pagingExtension.OriginalFieldExpression!, parentNode!.Field!.FieldParam!, parentNode!.ParentNode!.NextFieldContext!);

        // expression here is the adjusted Connection<T>(). This field (edges) is where we deal with the list again - field.Resolve
        foreach (var extension in pagingExtension.ExtensionsBeforePaging)
        {
            var res = extension.GetExpressionAndArguments(field, expression, argumentParam, arguments, context, parentNode, servicesPass, parameterReplacer, originalArgParam, compileContext);
            (expression, originalArgParam, argumentParam, arguments) = (res.Item1!, res.Item2, res.Item3!, res.Item4);
#pragma warning disable CS0618 // Type or member is obsolete
            expression = extension.GetExpression(field, expression, argumentParam, arguments, context, parentNode, servicesPass, parameterReplacer)!;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        if (servicesPass)
            return (expression, originalArgParam, argumentParam, arguments); // don't need to do paging as it is done already

        arguments ??= new { };

        // check and set up arguments
        if (arguments.Before != null && arguments.After != null)
            throw new EntityGraphQLArgumentException($"Field only supports either before or after being supplied, not both.");
        if (arguments.First != null && arguments.First < 0)
            throw new EntityGraphQLArgumentException($"first argument can not be less than 0.");
        if (arguments.Last != null && arguments.Last < 0)
            throw new EntityGraphQLArgumentException($"last argument can not be less than 0.");

        // deserialize cursors here once (not many times in the fields)
        arguments.AfterNum = ConnectionHelper.DeserializeCursor(arguments.After);
        arguments.BeforeNum = ConnectionHelper.DeserializeCursor(arguments.Before);

        if (pagingExtension.MaxPageSize.HasValue)
        {
            if (arguments.First != null && arguments.First > pagingExtension.MaxPageSize.Value)
                throw new EntityGraphQLArgumentException($"first argument can not be greater than {pagingExtension.MaxPageSize.Value}.");
            if (arguments.Last != null && arguments.Last > pagingExtension.MaxPageSize.Value)
                throw new EntityGraphQLArgumentException($"last argument can not be greater than {pagingExtension.MaxPageSize.Value}.");
        }

        if (arguments.First == null && arguments.Last == null && pagingExtension.DefaultPageSize != null)
            arguments.First = pagingExtension.DefaultPageSize;

        Expression? edgeExpression = Expression.Call(
            isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions),
            nameof(EnumerableExtensions.Take),
            [listType],
            Expression.Call(
                isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions),
                nameof(EnumerableExtensions.Skip),
                [listType],
                expression,
                Expression.Call(typeof(ConnectionHelper), nameof(ConnectionHelper.GetSkipNumber), null, argumentParam, Expression.Constant(true))
            ),
            Expression.Call(typeof(ConnectionHelper), nameof(ConnectionHelper.GetTakeNumber), null, argumentParam,
                Expression.Call(typeof(ConnectionHelper), nameof(ConnectionHelper.GetSkipNumber), null, argumentParam, Expression.Constant(false)))
        );

        // we have moved the expression from the parent node to here. We need to call the before callback
        if (parentNode?.IsRootField == true)
            BaseGraphQLField.HandleBeforeRootFieldExpressionBuild(
                compileContext,
                BaseGraphQLField.GetOperationName((BaseGraphQLField)parentNode),
                parentNode.Name!,
                servicesPass,
                parentNode.IsRootField,
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
        Expression baseExpression,
        Dictionary<IFieldKey, CompiledField> selectionExpressions,
        ParameterExpression? selectContextParam,
        ParameterExpression? argumentParam,
        bool servicesPass,
        ParameterReplacer parameterReplacer
    )
    {
        if (argumentParam == null)
            throw new EntityGraphQLCompilerException("ConnectionEdgeExtension requires an argument parameter to be passed in");

        if (servicesPass)
            return (baseExpression, selectionExpressions, selectContextParam);

        // we now know the fields they want to select so we rebuild the base expression
        // remove the above Select(new ConnectionEdge<T>(), ...)
        baseExpression = ((MethodCallExpression)baseExpression).Arguments[0];
        // remove null check as it is not required
        var nodeField = selectionExpressions.First(f => f.Key.SchemaName == "node").Value;
        var anonNewExpression = nodeField.Expression;
        if (nodeField.Expression.NodeType == ExpressionType.Conditional)
            anonNewExpression = ((ConditionalExpression)anonNewExpression).IfFalse;
        var anonType = anonNewExpression.Type;
        var edgeType = typeof(ConnectionEdge<>).MakeGenericType(anonType);
        var edgeParam = Expression.Parameter(edgeType, "newEdgeParam");
        var newNodeExpression = parameterReplacer.ReplaceByType(anonNewExpression, firstSelectParam.Type, firstSelectParam);

        baseExpression = Expression.Call(
            isQueryable ? typeof(Queryable) : typeof(Enumerable),
            nameof(Enumerable.Select),
            new Type[] { listType, edgeType },
            baseExpression,
            // we have the node selection from ConnectionEdgeNodeExtension we can insert into here for a nice EF compatible query
            Expression.Lambda(Expression.MemberInit(Expression.New(edgeType), new List<MemberBinding> { Expression.Bind(edgeType.GetProperty("Node")!, newNodeExpression) }), firstSelectParam)
        );

        var idxParam = Expression.Parameter(typeof(int), "cursor_idx");
        var offsetParam = Expression.Call(typeof(ConnectionHelper), nameof(ConnectionHelper.GetSkipNumber), null, argumentParam, Expression.Constant(false));
        // now select with cursor
        baseExpression = Expression.Call(
            typeof(Enumerable),
            "Select",
            new Type[] { edgeType, edgeType },
            baseExpression,
            Expression.Lambda(
                Expression.MemberInit(
                    Expression.New(edgeType),
                    new List<MemberBinding>
                    {
                        Expression.Bind(edgeType.GetProperty("Node")!, Expression.PropertyOrField(edgeParam, "Node")),
                        Expression.Bind(edgeType.GetProperty("Cursor")!, Expression.Call(typeof(ConnectionHelper), "GetCursor", null, argumentParam, idxParam, offsetParam))
                    }
                ),
                edgeParam,
                idxParam
            )
        );

        nodeField.Expression = Expression.PropertyOrField(edgeParam, "Node");
        if (selectionExpressions.Any(f => f.Key.Name == "cursor"))
            selectionExpressions.First(f => f.Key.Name == "cursor").Value.Expression = Expression.PropertyOrField(edgeParam, "Cursor");

        return (baseExpression, selectionExpressions, edgeParam);
    }
}
