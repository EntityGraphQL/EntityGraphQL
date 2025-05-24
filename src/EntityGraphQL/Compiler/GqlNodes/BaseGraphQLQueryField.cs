using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using EntityGraphQL.Compiler.Util;
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
        IReadOnlyDictionary<string, object?>? arguments
    )
        : base(schema, field, name, nextFieldContext, rootParameter, parentNode, arguments) { }

    protected BaseGraphQLQueryField(BaseGraphQLQueryField context, Expression? nextFieldContext)
        : base(context, nextFieldContext)
    {
        PossibleNextContextTypes = [.. context.PossibleNextContextTypes ?? []];
        // we don't populate ToSingleNode as GraphQLCollectionToSingleField handles that
    }

    protected bool NeedsServiceWrap(bool withoutServiceFields) => !withoutServiceFields && HasServices;

    protected virtual Dictionary<IFieldKey, CompiledField> GetSelectionFields(
        CompileContext compileContext,
        IServiceProvider? serviceProvider,
        List<GraphQLFragmentStatement> fragments,
        ParameterExpression? docParam,
        IPropertySetTrackingDto? docVariables,
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
                    HandleBulkResolverForField(compileContext, field, field.Field.BulkResolver, docParam, docVariables, replacer);
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
                    if (potentialMatch.FromType != null && subField.FromType.BaseTypesReadOnly.Contains(potentialMatch.FromType))
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void HandleBulkResolverForField(
        CompileContext compileContext,
        BaseGraphQLField field,
        IBulkFieldResolver bulkResolver,
        ParameterExpression? docParam,
        IPropertySetTrackingDto? docVariables,
        ParameterReplacer replacer
    )
    {
        // default do nothing
    }

    /// <summary>
    /// Default shared implementation for handling bulk resolvers
    /// </summary>
    protected static void DefaultHandleBulkResolverForField(
        CompileContext compileContext,
        BaseGraphQLField field,
        IBulkFieldResolver bulkResolver,
        ParameterExpression? docParam,
        IPropertySetTrackingDto? docVariables,
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
        List<IGraphQLNode> listExpressionPath = [];

        var parentNode = field.ParentNode;
        while (parentNode != null)
        {
            listExpressionPath.Insert(0, parentNode);
            parentNode = parentNode.ParentNode;
        }
        compileContext.AddBulkResolver(bulkResolver.Name, bulkResolver.DataSelector, (LambdaExpression)bulkFieldExpr, bulkResolver.ExtractedFields, listExpressionPath);
        compileContext.AddServices(field.Field!.Services);
    }
}
