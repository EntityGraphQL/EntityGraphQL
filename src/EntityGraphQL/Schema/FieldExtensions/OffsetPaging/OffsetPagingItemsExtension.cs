using System;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions;

public class OffsetPagingItemsExtension : BaseFieldExtension
{
    private readonly bool isQueryable;
    private readonly Type listType;

    public OffsetPagingItemsExtension(bool isQueryable, Type listType)
    {
        this.isQueryable = isQueryable;
        this.listType = listType;
    }

    public override (ParameterExpression? originalArgParam, ParameterExpression? newArgParam, object? argumentValue) ProcessArguments(ParameterExpression? originalArgParam, ParameterExpression? newArgParam, object? argumentValue, CompileContext? compileContext, IGraphQLNode? parentNode)
    {
        // We know we need the arguments from the parent field as that is where they are defined
        if (compileContext != null && parentNode != null)
        {
            newArgParam = compileContext.GetConstantParameterForField(parentNode.Field!) ?? throw new EntityGraphQLCompilerException($"Could not find arguments for field '{parentNode.Field!.Name}' in compile context.");
            argumentValue = compileContext.ConstantParameters[newArgParam];
            originalArgParam = parentNode.Field!.ArgumentsParameter;
        }
        return (originalArgParam, newArgParam, argumentValue);
    }

    public override Expression? GetExpression(IField field, Expression expression, ParameterExpression? argumentParam, dynamic? arguments, Expression context, IGraphQLNode? parentNode, bool servicesPass, ParameterReplacer parameterReplacer)
    {
        // we use the resolveExpression & extensions from our parent extension. We need to figure this out at runtime as the type this Items field
        // is on may be used in multiple places and have different arguments etc
        // See OffsetPagingTests.TestMultiUseWithArgs
        var offsetPagingExtension = (OffsetPagingExtension)parentNode!.Field!.Extensions.Find(e => e is OffsetPagingExtension)!;

        var resolveExpression = offsetPagingExtension.OriginalFieldExpression!;
        var originalFieldParam = parentNode.Field!.FieldParam!;
        Expression newItemsExp = servicesPass ? expression : parameterReplacer.Replace(resolveExpression, originalFieldParam, parentNode!.ParentNode!.NextFieldContext!);
        // other extensions defined on the original field need to run on the collection

        foreach (var extension in offsetPagingExtension.Extensions)
        {
            newItemsExp = extension.GetExpression(field, newItemsExp, argumentParam, arguments, context, parentNode, servicesPass, parameterReplacer)!;
        }

        if (servicesPass)
            return newItemsExp; // paging is done already

        if (argumentParam == null)
            throw new EntityGraphQLCompilerException("OffsetPagingItemsExtension requires an argument parameter to be passed in");

        // Build our items expression with the paging
        newItemsExp = Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), "Take", [listType],
            Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), "Skip", [listType],
                newItemsExp,
                Expression.PropertyOrField(argumentParam, "skip")
            ),
            Expression.PropertyOrField(argumentParam, "take")
        );

        return newItemsExp;
    }
}