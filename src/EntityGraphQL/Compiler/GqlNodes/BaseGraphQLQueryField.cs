using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

/// <summary>
/// Base class for both the ListSelectionField and ObjectProjectionField
/// </summary>
public abstract class BaseGraphQLQueryField : BaseGraphQLField
{
    /// <summary>
    /// Possible types for the next field context, introduce by interfaces or unions
    /// These are only needed after the for evaluation if a second one is required with service fields.
    /// They are dynamic types used to cast the base
    /// </summary>
    internal List<Type>? PossibleNextContextTypes { get; set; }
    public GraphQLCollectionToSingleField? ToSingleNode { get; set; }

    protected BaseGraphQLQueryField(
        ISchemaProvider schema,
        IField? field,
        string name,
        Expression? nextFieldContext,
        ParameterExpression? rootParameter,
        IGraphQLNode? parentNode,
        IReadOnlyDictionary<string, object>? arguments
    )
        : base(schema, field, name, nextFieldContext, rootParameter, parentNode, arguments) { }

    protected BaseGraphQLQueryField(BaseGraphQLQueryField context, Expression? nextFieldContext)
        : base(context, nextFieldContext) { }

    protected bool NeedsServiceWrap(bool withoutServiceFields) => !withoutServiceFields && HasServices;

    protected virtual Dictionary<IFieldKey, CompiledField> GetSelectionFields(
        CompileContext compileContext,
        IServiceProvider? serviceProvider,
        List<GraphQLFragmentStatement> fragments,
        ParameterExpression? docParam,
        object? docVariables,
        bool withoutServiceFields,
        Expression nextFieldContext,
        ParameterExpression schemaContext,
        bool contextChanged,
        ParameterReplacer replacer
    )
    {
        var selectionFields = new Dictionary<IFieldKey, CompiledField>();

        foreach (var field in QueryFields)
        {
            // not needed if it is a list-to-single node
            if (field.Field?.BulkResolver != null && (field.ParentNode as BaseGraphQLQueryField)?.ToSingleNode == null)
            {
                if (withoutServiceFields)
                {
                    // This is where we have the information to build the bulk resolver data loading expression
                    var newArgParam = HandleBulkResolverForField(compileContext, field, field.Field.BulkResolver, docParam, docVariables, replacer);
                }
            }
            // Might be a fragment that expands into many fields hence the Expand
            // or a service field that we expand into the required fields for input
            foreach (var subField in field.Expand(compileContext, fragments, withoutServiceFields, nextFieldContext, docParam, docVariables))
            {
                // fragments might be fragments on the actually type whereas the context is a interface
                // we do not need to change the context in this case
                var actualNextFieldContext = nextFieldContext;
                if (
                    !contextChanged
                    && subField.RootParameter != null
                    && actualNextFieldContext.Type != subField.RootParameter.Type
                    && (field is GraphQLInlineFragmentField || field is GraphQLFragmentSpreadField)
                    && (subField.FromType?.BaseTypesReadOnly.Any() == true || Field?.ReturnType.SchemaType.GqlType == GqlTypes.Union)
                )
                {
                    // we can do the convert here and avoid have to do a replace later
                    actualNextFieldContext = Expression.Convert(actualNextFieldContext, subField.RootParameter.Type)!;
                }

                var fieldExp = subField.GetNodeExpression(
                    compileContext,
                    serviceProvider,
                    fragments,
                    docParam,
                    docVariables,
                    schemaContext,
                    withoutServiceFields,
                    actualNextFieldContext,
                    PossibleNextContextTypes,
                    contextChanged,
                    replacer
                );
                if (fieldExp == null)
                    continue;

                var potentialMatch = selectionFields.Keys.FirstOrDefault(f => f.Name == subField.Name);
                if (potentialMatch != null && subField.FromType != null)
                {
                    // if we have a match, we need to check if the types are the same
                    // if they are, we can just use the existing field
                    if (potentialMatch.FromType?.BaseTypesReadOnly.Contains(subField.FromType) == true)
                    {
                        continue;
                    }
                    if (potentialMatch.FromType != null && subField.FromType.BaseTypesReadOnly.Contains(potentialMatch.FromType) == true)
                    {
                        // replace - use the non-base type field
                        selectionFields.Remove(potentialMatch);
                        selectionFields[subField] = new CompiledField(subField, fieldExp);
                        continue;
                    }
                }
                selectionFields[subField] = new CompiledField(subField, fieldExp);
            }
        }

        return selectionFields;
    }

    protected virtual ParameterExpression? HandleBulkResolverForField(
        CompileContext compileContext,
        BaseGraphQLField field,
        IBulkFieldResolver bulkResolver,
        ParameterExpression? docParam,
        object? docVariables,
        ParameterReplacer replacer
    )
    {
        // default do nothing
        return field.Field?.ArgumentsParameter;
    }

    /// <summary>
    /// Default shared implementation for handling bulk resolvers
    /// </summary>
    protected ParameterExpression? DefaultHandleBulkResolverForField(
        CompileContext compileContext,
        BaseGraphQLField field,
        IBulkFieldResolver bulkResolver,
        ParameterExpression? docParam,
        object? docVariables,
        ParameterReplacer replacer
    )
    {
        // Need the args that may be used in the bulk resolver expression
        var argumentValue = default(object);
        var validationErrors = new List<string>();
        var bulkFieldArgParam = bulkResolver.BulkArgParam;
        var newArgParam = bulkFieldArgParam != null ? Expression.Parameter(bulkFieldArgParam!.Type, $"{bulkFieldArgParam.Name}_exec") : null;
        compileContext.AddArgsToCompileContext(field.Field!, field.Arguments, docParam, docVariables, ref argumentValue, validationErrors, newArgParam);

        // replace the arg param after extensions (don't rely on extensions to do this)
        Expression bulkFieldExpr = bulkResolver.FieldExpression;

        GraphQLHelper.ValidateAndReplaceFieldArgs(field.Field!, bulkFieldArgParam, replacer, ref argumentValue, ref bulkFieldExpr, validationErrors, newArgParam);
        Expression listExpression = field.RootParameter!;
        var parentNode = field.ParentNode;
        var rootParameter = field.RootParameter!;
        var isList = false;

        while (parentNode != null)
        {
            Type typeDotnet = Field!.ReturnType.SchemaType.TypeDotnet;
            if (parentNode is GraphQLListSelectionField parentListNode)
            {
                if (parentListNode.ToSingleNode != null)
                {
                    listExpression = replacer.Replace(listExpression, rootParameter!, parentListNode.ToSingleNode.NextFieldContext!);
                    var nullCheck = Expression.MakeBinary(
                        ExpressionType.Equal,
                        parentListNode.ToSingleNode.NextFieldContext!,
                        Expression.Constant(null, parentListNode.ToSingleNode.NextFieldContext!.Type)
                    );
                    listExpression = Expression.Condition(nullCheck, Expression.NewArrayInit(typeDotnet), listExpression, typeof(IEnumerable<>).MakeGenericType(typeDotnet));
                    rootParameter = parentNode.RootParameter!;
                }
                else
                {
                    // We can do SelectManyWithNullCheck in memory as services are post EF
                    string selectMethod = isList ? nameof(EnumerableExtensions.SelectManyWithNullCheck) : nameof(EnumerableExtensions.SelectWithNullCheck);
                    var parentListExpression = parentListNode.ListExpression!;
                    foreach (var extension in parentNode.Field!.Extensions)
                    {
                        parentListExpression = extension.GetListExpressionForBulkResolve(parentListExpression);
                    }
                    listExpression = Expression.Call(
                        typeof(EnumerableExtensions),
                        selectMethod,
                        [rootParameter!.Type, typeDotnet],
                        parentListExpression,
                        Expression.Lambda(listExpression, rootParameter!)
                    );
                    rootParameter = parentNode.RootParameter!;
                }
                isList = true;
            }
            else if (parentNode is GraphQLObjectProjectionField parentObjectNode)
            {
                listExpression = replacer.Replace(listExpression, rootParameter!, parentObjectNode.NextFieldContext!);
                if (isList)
                {
                    var nullCheck = Expression.MakeBinary(ExpressionType.Equal, parentObjectNode.NextFieldContext!, Expression.Constant(null, parentObjectNode.NextFieldContext!.Type));
                    listExpression = Expression.Condition(nullCheck, Expression.NewArrayInit(typeDotnet), listExpression, typeof(IEnumerable<>).MakeGenericType(typeDotnet));
                }
                rootParameter = parentNode.RootParameter!;
            }
            parentNode = parentNode.ParentNode;
        }
        compileContext.AddBulkResolver(bulkResolver.Name, bulkResolver.DataSelector, (LambdaExpression)bulkFieldExpr, listExpression, bulkResolver.ExtractedFields);
        compileContext.AddServices(field.Field!.Services);
        return newArgParam;
    }
}
