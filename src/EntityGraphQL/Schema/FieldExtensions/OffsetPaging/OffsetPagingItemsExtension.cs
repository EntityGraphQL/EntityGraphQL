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
            var res = extension.GetExpressionAndArguments(field, newItemsExp, argumentParam, arguments, context, parentNode, servicesPass, parameterReplacer, originalArgParam, compileContext);
            (newItemsExp, originalArgParam, argumentParam, arguments) = (res.Item1!, res.Item2, res.Item3!, res.Item4);
#pragma warning disable CS0618 // Type or member is obsolete
            newItemsExp = extension.GetExpression(field, newItemsExp, argumentParam, arguments, context, parentNode, servicesPass, parameterReplacer)!;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        if (servicesPass)
            return (newItemsExp, originalArgParam, argumentParam, arguments); // paging is done already

        if (argumentParam == null)
            throw new EntityGraphQLCompilerException("OffsetPagingItemsExtension requires an argument parameter to be passed in");

        // Build our items expression with the paging
        newItemsExp = Expression.Call(
            isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions),
            nameof(EnumerableExtensions.Take),
            [listType],
            Expression.Call(
                isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions),
                nameof(EnumerableExtensions.Skip),
                [listType],
                newItemsExp,
                Expression.PropertyOrField(argumentParam, "skip")
            ),
            Expression.PropertyOrField(argumentParam, "take")
        );

        // we have moved the expression from the parent node to here. We need to call the before callback
        if (parentNode?.IsRootField == true)
            BaseGraphQLField.HandleBeforeRootFieldExpressionBuild(
                compileContext,
                BaseGraphQLField.GetOperationName((BaseGraphQLField)parentNode),
                parentNode.Name!,
                servicesPass,
                parentNode.IsRootField,
                ref newItemsExp
            );

        return (newItemsExp, originalArgParam, argumentParam, arguments);
    }
}
