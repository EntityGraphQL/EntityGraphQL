using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions;

public class OffsetPagingItemsExtension : BaseFieldExtension
{
    private readonly bool isQueryable;
    private readonly Type listType;
    private readonly List<IFieldExtension> extensions;
    private readonly ParameterExpression originalFieldParam;

    public OffsetPagingItemsExtension(bool isQueryable, Type listType, List<IFieldExtension> extensions, ParameterExpression fieldParam)
    {
        this.isQueryable = isQueryable;
        this.listType = listType;
        this.extensions = extensions;
        this.originalFieldParam = fieldParam;
    }

    public override Expression? GetExpression(IField field, Expression expression, ParameterExpression? argumentParam, dynamic? arguments, Expression context, IGraphQLNode? parentNode, bool servicesPass, ParameterReplacer parameterReplacer)
    {
        // other extensions expect to run on the collection not our new shape
        Expression newItemsExp = servicesPass ? expression : parameterReplacer.Replace(field.ResolveExpression!, this.originalFieldParam, parentNode!.ParentNode!.NextFieldContext!);
        // apply other expressions 
        foreach (var extension in extensions)
        {
            newItemsExp = extension.GetExpression(field, newItemsExp, argumentParam, arguments, context, parentNode, servicesPass, parameterReplacer);
        }

        if (servicesPass)
            return newItemsExp; // paging is done already

        if (argumentParam == null)
            throw new EntityGraphQLCompilerException("OffsetPagingItemsExtension requires an argument parameter to be passed in");

        // Build our items expression with the paging
        newItemsExp = Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), "Take", new Type[] { listType },
            Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), "Skip", new Type[] { listType },
                newItemsExp,
                Expression.PropertyOrField(argumentParam, "skip")
            ),
            Expression.PropertyOrField(argumentParam, "take")
        );

        return newItemsExp;
    }
}