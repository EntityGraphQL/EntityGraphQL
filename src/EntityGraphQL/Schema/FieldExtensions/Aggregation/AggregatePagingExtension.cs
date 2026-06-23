using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Schema.FieldExtensions;

/// <summary>
/// Placed on the synthetic "aggregate" field that lives on a paging wrapper (OffsetPage/Connection).
/// Supplies the source collection (the full, filtered, pre-paging collection) as the aggregate field's
/// context so the object-projection machinery can build new { count = coll.Count(), min = ... } from it.
///
/// This mirrors <see cref="OffsetPagingItemsExtension"/>'s context resolution but stops short of Skip/Take
/// (aggregates are over the whole collection) and only re-applies filter extensions (sort is irrelevant to
/// aggregates and an OrderBy in an aggregate subquery does not translate to SQL).
/// </summary>
public class AggregatePagingExtension : BaseFieldExtension
{
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

        var parentField = fieldNode.ParentNode!.Field!;

        // paging built in the first pass for non-service fields; in the second pass the shape is already set
        if (servicesPass && parentField.Services.Count == 0)
            return (expression, originalArgParam, argumentParam, arguments);

        var (originalFieldExpression, beforePagingExtensions) = GetPagingInfo(parentField);
        if (originalFieldExpression == null)
            return (expression, originalArgParam, argumentParam, arguments);

        // arguments are defined on the parent paged field (filter etc.)
        argumentParam =
            compileContext.GetConstantParameterForField(parentField)
            ?? throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, $"Could not find arguments for field '{parentField.Name}' in compile context.");
        arguments = compileContext.ConstantParameters[argumentParam];
        originalArgParam = parentField.ArgumentsParameter;

        // remap the original collection expression onto the actual grandparent context (e.g. the DbContext).
        // In the services pass the grandparent element type is an anonymous type built in the first pass, so we
        // remap service-extracted member accesses onto it (mirrors OffsetPagingItemsExtension). This lets a
        // service-backed collection resolver (e.g. ctx.People.Where(p => svc.Keep(p))) aggregate over the same set.
        var grandparentContext = fieldNode.ParentNode!.ParentNode!.NextFieldContext!;
        Expression coll;
        if (servicesPass && parentField.Services.Count > 0)
        {
            compileContext.AddServices(parentField.Services);
            var replacement = grandparentContext is ParameterExpression gpParam ? compileContext.GetFieldContextReplacement(gpParam) : null;
            if (replacement != null && parentField.ExtractedFieldsFromServices != null)
            {
                var expReplacer = new ExpressionReplacer(parentField.ExtractedFieldsFromServices, replacement, false, false, null);
                coll = expReplacer.Replace(originalFieldExpression);
                coll = parameterReplacer.Replace(coll, parentField.FieldParam!, replacement);
            }
            else
            {
                coll = parameterReplacer.Replace(originalFieldExpression, parentField.FieldParam!, (Expression?)replacement ?? grandparentContext);
            }
        }
        else
        {
            coll = parameterReplacer.Replace(originalFieldExpression, parentField.FieldParam!, grandparentContext);
        }

        // re-apply filter extensions so the aggregate is over the same filtered set as the page (but not sort/paging)
        foreach (var extension in beforePagingExtensions.Where(e => e is FilterExpressionExtension))
        {
            var res = extension.GetExpressionAndArguments(
                field,
                new FieldExtensionExpressionContext
                {
                    FieldNode = fieldNode,
                    Expression = coll,
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
            (coll, originalArgParam, argumentParam, arguments) = (res.Item1!, res.Item2, res.Item3!, res.Item4);
        }

        return (coll, originalArgParam, argumentParam, arguments);
    }

    private static (Expression? original, List<IFieldExtension> before) GetPagingInfo(IField parentField)
    {
        var offset = parentField.Extensions.OfType<OffsetPagingExtension>().FirstOrDefault();
        if (offset != null)
            return (offset.OriginalFieldExpression, offset.Extensions);

        var connection = parentField.Extensions.OfType<ConnectionPagingExtension>().FirstOrDefault();
        if (connection != null)
            return (connection.OriginalFieldExpression, connection.ExtensionsBeforePaging);

        return (null, []);
    }
}
