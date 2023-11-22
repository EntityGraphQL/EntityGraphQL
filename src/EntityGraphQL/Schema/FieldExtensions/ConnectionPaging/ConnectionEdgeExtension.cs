using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions;

internal class ConnectionEdgeExtension : BaseFieldExtension
{
    private readonly ParameterExpression originalFieldParam;
    private readonly int? defaultPageSize;
    private readonly int? maxPageSize;
    private readonly Type listType;
    private readonly ParameterExpression firstSelectParam;
    private readonly bool isQueryable;
    private readonly List<IFieldExtension> extensions;

    public ConnectionEdgeExtension(Type listType, bool isQueryable, List<IFieldExtension> extensions, ParameterExpression fieldParam, int? defaultPageSize, int? maxPageSize)
    {
        this.listType = listType;
        this.isQueryable = isQueryable;
        this.extensions = extensions;
        firstSelectParam = Expression.Parameter(listType, "edgeNode");
        this.originalFieldParam = fieldParam;
        this.defaultPageSize = defaultPageSize;
        this.maxPageSize = maxPageSize;
    }

    public override Expression? GetExpression(IField field, Expression expression, ParameterExpression? argumentParam, dynamic? arguments, Expression context, IGraphQLNode? parentNode, bool servicesPass, ParameterReplacer parameterReplacer)
    {
        if (argumentParam == null)
            throw new EntityGraphQLCompilerException("ConnectionEdgeExtension requires an argument parameter to be passed in");
        // field.Resolve will be built with the original field context and needs to be updated
        expression = servicesPass ? expression : parameterReplacer.Replace(field.ResolveExpression!, originalFieldParam, parentNode!.ParentNode!.NextFieldContext!);
        // expression here is the adjusted Connection<T>(). This field (edges) is where we deal with the list again - field.Resolve
        foreach (var extension in extensions)
        {
            expression = extension.GetExpression(field, expression, argumentParam, arguments, context, parentNode, servicesPass, parameterReplacer);
        }

        if (servicesPass)
            return expression; // don't need to do paging as it is done already

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

        if (maxPageSize.HasValue)
        {
            if (arguments.First != null && arguments.First > maxPageSize.Value)
                throw new EntityGraphQLArgumentException($"first argument can not be greater than {maxPageSize.Value}.");
            if (arguments.Last != null && arguments.Last > maxPageSize.Value)
                throw new EntityGraphQLArgumentException($"last argument can not be greater than {maxPageSize.Value}.");
        }

        if (arguments.First == null && arguments.Last == null && defaultPageSize != null)
            arguments.First = defaultPageSize;

        var edgeExpression = Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), "Take", new Type[] { listType },
            Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), "Skip", new Type[] { listType },
                expression,
                Expression.Call(typeof(ConnectionHelper), "GetSkipNumber", null, argumentParam)
            ),
            Expression.Call(typeof(ConnectionHelper), "GetTakeNumber", null, argumentParam)
        );

        // First we select the edge node as the full object
        // we later change this to a anonymous object to not have the full table returned from EF
        // This happens later as we don't know what the query has selected yet
        expression = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Select", new Type[] { listType, field.ReturnType.SchemaType.TypeDotnet },
            edgeExpression,
            // we have the node selection from ConnectionEdgeNodeExtension we can insert into here for a nice EF compatible query
            Expression.Lambda(Expression.MemberInit(Expression.New(field.ReturnType.SchemaType.TypeDotnet),
                new List<MemberBinding>
                {
                    Expression.Bind(field.ReturnType.SchemaType.TypeDotnet.GetProperty("Node")!, firstSelectParam)
                }
            ), firstSelectParam)
        );

        return expression;
    }

    public override (Expression, ParameterExpression?) ProcessExpressionPreSelection(Expression baseExpression, ParameterExpression? listTypeParam, ParameterReplacer parameterReplacer)
    {
        foreach (var extension in extensions)
        {
            (baseExpression, listTypeParam) = extension.ProcessExpressionPreSelection(baseExpression, listTypeParam, parameterReplacer);
        }

        return (baseExpression, listTypeParam);
    }
    public override (Expression baseExpression, Dictionary<IFieldKey, CompiledField> selectionExpressions, ParameterExpression? selectContextParam) ProcessExpressionSelection(Expression baseExpression, Dictionary<IFieldKey, CompiledField> selectionExpressions, ParameterExpression? selectContextParam, ParameterExpression? argumentsParam, bool servicesPass, ParameterReplacer parameterReplacer)
    {
        if (argumentsParam == null)
            throw new EntityGraphQLCompilerException("ConnectionEdgeExtension requires an argument parameter to be passed in");
        foreach (var extension in extensions)
        {
            (baseExpression, selectionExpressions, selectContextParam) = extension.ProcessExpressionSelection(baseExpression, selectionExpressions, selectContextParam, argumentsParam, servicesPass, parameterReplacer);
        }

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

        baseExpression = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Select", new Type[] { listType, edgeType },
            baseExpression,
            // we have the node selection from ConnectionEdgeNodeExtension we can insert into here for a nice EF compatible query
            Expression.Lambda(Expression.MemberInit(Expression.New(edgeType),
                new List<MemberBinding>
                {
                    Expression.Bind(edgeType.GetProperty("Node")!, newNodeExpression)
                }
            ), firstSelectParam)
        );

        var idxParam = Expression.Parameter(typeof(int), "cursor_idx");
        // now select with cursor
        baseExpression = Expression.Call(typeof(Enumerable), "Select", new Type[] { edgeType, edgeType },
            baseExpression,
            Expression.Lambda(
                Expression.MemberInit(Expression.New(edgeType),
                    new List<MemberBinding>
                    {
                        Expression.Bind(edgeType.GetProperty("Node")!, Expression.PropertyOrField(edgeParam, "Node")),
                        Expression.Bind(edgeType.GetProperty("Cursor")!, Expression.Call(typeof(ConnectionHelper), "GetCursor", null, argumentsParam, idxParam))
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