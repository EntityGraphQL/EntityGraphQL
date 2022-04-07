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
    /// <summary>
    /// This is passed down from the original field
    /// </summary>
    private readonly ParameterExpression argsExpression;

    private readonly ParameterExpression argumentParam;
    private readonly Type listType;
    private readonly ParameterExpression firstSelectParam;
    private readonly bool isQueryable;
    private readonly List<IFieldExtension> extensions;

    public ConnectionEdgeExtension(Type listType, bool isQueryable, ParameterExpression argsExpression, ParameterExpression argumentParam, List<IFieldExtension> extensions)
    {
        this.listType = listType;
        this.isQueryable = isQueryable;
        this.extensions = extensions;
        this.argsExpression = argsExpression;
        firstSelectParam = Expression.Parameter(listType, "edgeNode");
        this.argumentParam = argumentParam;
    }

    public override Expression GetExpression(Field field, Expression expression, ParameterExpression argExpression, dynamic arguments, Expression context, bool servicesPass, ParameterReplacer parameterReplacer)
    {
        expression = servicesPass ? expression : field.Resolve;
        // expression here is the adjusted Connection<T>(). This field (edges) is where we deal with the list again - field.Resolve
        foreach (var extension in extensions)
        {
            expression = extension.GetExpression(field, expression, argExpression, arguments, context, servicesPass, parameterReplacer);
        }

        if (servicesPass)
            return expression; // don't need to do paging as it is done already

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
                    Expression.Bind(field.ReturnType.SchemaType.TypeDotnet.GetProperty("Node"), firstSelectParam)
                }
            ), firstSelectParam)
        );

        return expression;
    }

    public override (Expression, ParameterExpression) ProcessExpressionPreSelection(GraphQLFieldType fieldType, Expression baseExpression, ParameterExpression listTypeParam, ParameterReplacer parameterReplacer)
    {
        foreach (var extension in extensions)
        {
            (baseExpression, listTypeParam) = extension.ProcessExpressionPreSelection(fieldType, baseExpression, listTypeParam, parameterReplacer);
        }

        return (baseExpression, listTypeParam);
    }
    public override (Expression baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam) ProcessExpressionSelection(GraphQLFieldType fieldType, Expression baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam, bool servicesPass, ParameterReplacer parameterReplacer)
    {
        foreach (var extension in extensions)
        {
            (baseExpression, selectionExpressions, selectContextParam) = extension.ProcessExpressionSelection(fieldType, baseExpression, selectionExpressions, selectContextParam, servicesPass, parameterReplacer);
        }

        if (servicesPass)
            return (baseExpression, selectionExpressions, selectContextParam);

        // we now know the fields they want to select so we rebuild the base expression
        // remove the above Select(new ConnectionEdge<T>(), ...)
        baseExpression = ((MethodCallExpression)baseExpression).Arguments[0];
        // remove null check as it is not required
        var anonNewExpression = ((ConditionalExpression)selectionExpressions["node"].Expression).IfFalse;
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
                    Expression.Bind(edgeType.GetProperty("Node"), newNodeExpression)
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
                        Expression.Bind(edgeType.GetProperty("Node"), Expression.PropertyOrField(edgeParam, "Node")),
                        Expression.Bind(edgeType.GetProperty("Cursor"), Expression.Call(typeof(ConnectionHelper), "GetCursor", null, argsExpression, idxParam))
                    }
                ),
                edgeParam,
                idxParam
            )
        );

        selectionExpressions["node"].Expression = Expression.PropertyOrField(edgeParam, "Node");
        if (selectionExpressions.ContainsKey("cursor"))
            selectionExpressions["cursor"].Expression = Expression.PropertyOrField(edgeParam, "Cursor");

        return (baseExpression, selectionExpressions, edgeParam);
    }
}