using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Compiler;

public class CompiledBulkFieldResolver(
    string name,
    LambdaExpression dataSelection,
    LambdaExpression fieldExpression,
    IEnumerable<GraphQLExtractedField> extractedFields,
    List<IGraphQLNode> listExpressionPath
)
{
    public string Name { get; private set; } = name;
    public LambdaExpression DataSelection { get; private set; } = dataSelection;
    public LambdaExpression FieldExpression { get; private set; } = fieldExpression;
    public IEnumerable<GraphQLExtractedField> ExtractedFields { get; } = extractedFields;
    public List<IGraphQLNode> ListExpressionPath { get; } = listExpressionPath;

    public Expression GetBulkSelectionExpression(Expression newContextParam, List<IGraphQLNode> listExpressionPath, ParameterReplacer replacer, bool isRoot = true)
    {
        Expression currentContextExpression = newContextParam;

        for (int i = 0; i < listExpressionPath.Count; i++)
        {
            IGraphQLNode parentNode = listExpressionPath[i];
            // we should be able to select from the new context directly
            if (i == 0 && isRoot)
            {
                currentContextExpression = newContextParam;
                // Mutation fields are followed by their own ResultSelection node (same name, distinct
                // instance). Skip that structural duplicate so navigation starts at the first genuine
                // child field rather than trying to read the root field name off its own result.
                // Match by reference — not name — so a child aliased to the root name is not skipped.
                if (parentNode is GraphQLMutationField mutationNode && i + 1 < listExpressionPath.Count && ReferenceEquals(listExpressionPath[i + 1], mutationNode.ResultSelection))
                    i++;
            }
            else
            {
                var isCurrentContextList = currentContextExpression.Type.IsEnumerableOrArray();
                Expression param = isCurrentContextList ? Expression.Parameter(currentContextExpression.Type.GetEnumerableOrArrayType()!, "bulk_listExp") : currentContextExpression;
                Expression selection = Expression.PropertyOrField(param, parentNode.Name ?? parentNode.Field!.Name);
                var isSelectionList = selection.Type.IsEnumerableOrArray();
                // null for a single-object selection - only list selections have an element type
                var selectElementType = selection.Type.GetEnumerableOrArrayType();
                if (i > 0)
                {
                    if (isSelectionList)
                    {
                        selection = Expression.Condition(
                            Expression.Equal(param, Expression.Constant(null, param.Type)),
                            Expression.NewArrayInit(selectElementType!),
                            selection,
                            typeof(IEnumerable<>).MakeGenericType(selectElementType!)
                        );
                    }
                    else if (!selection.Type.IsValueType)
                    {
                        // single-object navigation - a null parent yields null. The nulls are filtered out
                        // before the bulk key selection (Where(x => x != null) when collecting bulk args)
                        selection = Expression.Condition(Expression.Equal(param, Expression.Constant(null, param.Type)), Expression.Constant(null, selection.Type), selection);
                    }
                }

                if (parentNode is GraphQLListSelectionField parentListNode)
                {
                    if (parentListNode.ToSingleNode != null)
                    {
                        currentContextExpression = Expression.PropertyOrField(currentContextExpression, parentNode.Name ?? parentNode.Field!.Name);
                    }
                    else
                    {
                        if (currentContextExpression.Type.IsEnumerableOrArray())
                        {
                            isSelectionList = selection.Type.IsEnumerableOrArray();
                            string selectMethod = isSelectionList ? nameof(EnumerableExtensions.SelectManyWithNullCheck) : nameof(EnumerableExtensions.SelectWithNullCheck);

                            currentContextExpression = Expression.Call(
                                typeof(EnumerableExtensions),
                                selectMethod,
                                [param.Type, isSelectionList ? selectElementType! : selection.Type],
                                currentContextExpression,
                                Expression.Lambda(selection, (ParameterExpression)param),
                                Expression.Constant(true)
                            );
                        }
                        else
                        {
                            currentContextExpression = selection;
                        }
                        var remainder = listExpressionPath.GetRange(i + 1, listExpressionPath.Count - i - 1);
                        if (remainder.Count > 0)
                        {
                            param = Expression.Parameter(selectElementType!, "bulk_sel");
                            selection = GetBulkSelectionExpression(param, remainder, replacer, false);
                            // We can do SelectManyWithNullCheck in memory as services are post EF
                            isSelectionList = selection.Type.IsEnumerableOrArray();
                            string selectMethod = isSelectionList ? nameof(EnumerableExtensions.SelectManyWithNullCheck) : nameof(EnumerableExtensions.SelectWithNullCheck);

                            currentContextExpression = Expression.Call(
                                typeof(EnumerableExtensions),
                                selectMethod,
                                [param.Type, isSelectionList ? selection.Type.GetEnumerableOrArrayType()! : selection.Type],
                                currentContextExpression,
                                Expression.Lambda(selection, (ParameterExpression)param),
                                Expression.Constant(true)
                            );
                            return currentContextExpression;
                        }
                    }
                }
                else if (parentNode is GraphQLObjectProjectionField parentObjectNode)
                {
                    if (isCurrentContextList)
                    {
                        // The context is a list (e.g. a root list field, or the result of an earlier list
                        // navigation) and we are navigating an object/collection field on each element.
                        // Project it out per element (and flatten if the field itself is a collection) so
                        // the bulk data selector still sees a flat list of the target objects. Without this
                        // the single-object handling below produced an expression referencing an unbound
                        // element parameter (or threw building IEnumerable<null>).
                        string selectMethod = isSelectionList ? nameof(EnumerableExtensions.SelectManyWithNullCheck) : nameof(EnumerableExtensions.SelectWithNullCheck);
                        currentContextExpression = Expression.Call(
                            typeof(EnumerableExtensions),
                            selectMethod,
                            [param.Type, isSelectionList ? selectElementType! : selection.Type],
                            currentContextExpression,
                            Expression.Lambda(selection, (ParameterExpression)param),
                            Expression.Constant(true)
                        );
                    }
                    else if (isSelectionList)
                    {
                        var nullCheck = Expression.MakeBinary(ExpressionType.Equal, currentContextExpression, Expression.Constant(null, currentContextExpression.Type));
                        currentContextExpression = Expression.Condition(
                            nullCheck,
                            Expression.NewArrayInit(selectElementType!),
                            currentContextExpression,
                            typeof(IEnumerable<>).MakeGenericType(selectElementType!)
                        );
                    }
                    else
                    {
                        currentContextExpression = selection;
                    }
                }
                else
                {
                    currentContextExpression = selection;
                }
            }
        }
        return currentContextExpression;
    }
}
