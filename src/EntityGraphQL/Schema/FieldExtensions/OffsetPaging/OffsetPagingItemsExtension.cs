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

        // we use the resolveExpression & extensions from our parent extension. We need to figure this out at runtime as the type this Items field
        // is on may be used in multiple places and have different arguments etc
        // See OffsetPagingTests.TestMultiUseWithArgs
        var offsetPagingExtension = (OffsetPagingExtension)fieldNode.ParentNode!.Field!.Extensions.Find(e => e is OffsetPagingExtension)!;
        var parentField = fieldNode.ParentNode!.Field!;

        // For fields WITHOUT services in second pass: skip (paging done in first pass).
        // For service-backed paging fields the parent (e.g. pagedItems) returns early from GetFieldExpression
        // in the first pass, so items is never reached then — paging is always built in the second pass.
        if (servicesPass && parentField.Services.Count == 0)
            return (expression, originalArgParam, argumentParam, arguments);

        // Build the paging expression using the original field expression.
        // This happens in first pass for non-service fields, or second pass for service fields.
        // In the services pass (second pass), the grandparent list's element type has changed to an
        // anonymous type. We look up the replacement context stored by GraphQLListSelectionField and
        // use ExpressionReplacer to correctly remap member accesses (e.g. dir.Id → anonElem.id).
        var originalFieldParam = parentField.FieldParam!;
        var grandparentContext = fieldNode.ParentNode!.ParentNode!.NextFieldContext;
        Expression newItemsExp;
        if (servicesPass && parentField.Services.Count > 0 && grandparentContext is ParameterExpression grandparentParam)
        {
            var replacement = compileContext.GetFieldContextReplacement(grandparentParam);
            if (replacement != null && parentField.ExtractedFieldsFromServices != null)
            {
                var expReplacer = new ExpressionReplacer(parentField.ExtractedFieldsFromServices, replacement, false, false, null);
                newItemsExp = expReplacer.Replace(offsetPagingExtension.OriginalFieldExpression!);
                newItemsExp = parameterReplacer.Replace(newItemsExp, originalFieldParam, replacement);
            }
            else if (replacement != null)
            {
                newItemsExp = parameterReplacer.Replace(offsetPagingExtension.OriginalFieldExpression!, originalFieldParam, replacement);
            }
            else
            {
                newItemsExp = parameterReplacer.Replace(offsetPagingExtension.OriginalFieldExpression!, originalFieldParam, grandparentContext!);
            }
        }
        else
        {
            newItemsExp = parameterReplacer.Replace(offsetPagingExtension.OriginalFieldExpression!, originalFieldParam, grandparentContext!);
        }

        // other extensions defined on the original field need to run on the collection
        foreach (var extension in offsetPagingExtension.Extensions)
        {
            var res = extension.GetExpressionAndArguments(
                field,
                new FieldExtensionExpressionContext
                {
                    FieldNode = fieldNode,
                    Expression = newItemsExp,
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
            (newItemsExp, originalArgParam, argumentParam, arguments) = (res.Item1!, res.Item2, res.Item3!, res.Item4);
        }

        if (argumentParam == null)
            throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, "OffsetPagingItemsExtension requires an argument parameter to be passed in");

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
        if (fieldNode.ParentNode?.IsRootField == true)
            BaseGraphQLField.HandleBeforeRootFieldExpressionBuild(
                compileContext,
                BaseGraphQLField.GetOperationName((BaseGraphQLField)fieldNode.ParentNode),
                fieldNode.ParentNode.Name!,
                servicesPass,
                fieldNode.ParentNode.IsRootField,
                ref newItemsExp
            );

        return (newItemsExp, originalArgParam, argumentParam, arguments);
    }
}
