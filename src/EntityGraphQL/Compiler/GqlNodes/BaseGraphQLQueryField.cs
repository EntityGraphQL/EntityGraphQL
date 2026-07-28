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
    // Note: the possible types for the next field context (introduced by interfaces or unions) are per-request
    // compile state stored on the CompileContext keyed by this node - this node is part of the cached document
    // shared across concurrent requests so per-request state must not be stored on it
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
        // a copy still belongs to the same list-to-single selection. GraphQLCollectionToSingleField sets
        // this on the nodes it wraps, but the per-compile copy made to build the Select().First() chain
        // never goes through that constructor and would otherwise look like a plain list selection.
        //
        // Two things read it and both need a copy to answer like the original:
        //  - the bulk resolver's to-single check (IsSelectedOnToSingleNode). The second (services) pass
        //    compiles copies, so a copy that dropped this would answer differently from the original the
        //    first pass registered against - the same registration/lookup disagreement that made a bulk
        //    field selected through a fragment throw for a key that was never loaded.
        //  - CompiledBulkFieldResolver, which uses it to decide whether the bulk list expression navigates
        //    with PropertyOrField (a single item) or Select..WithNullCheck (a list).
        ToSingleNode = context.ToSingleNode;
    }

    protected bool NeedsServiceWrap(bool withoutServiceFields) => !withoutServiceFields && HasServices;

    protected virtual Dictionary<IFieldKey, CompiledField> GetSelectionFields(
        CompileContext compileContext,
        IServiceProvider? serviceProvider,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        ParameterExpression? docParam,
        IArgumentsTracker? docVariables,
        bool withoutServiceFields,
        Expression nextFieldContext,
        ParameterExpression schemaContext,
        bool contextChanged,
        ParameterReplacer replacer
    )
    {
        var selectionFields = new Dictionary<IFieldKey, CompiledField>();

        // this node is part of the selection path bulk resolvers snapshot for their list expression path
        compileContext.PushSelectionPathNode(this);
        try
        {
            foreach (var field in QueryFields)
            {
                if (withoutServiceFields)
                {
                    // This is where we have the information to build the bulk resolver data loading expression.
                    // Must happen before Expand - a bulk field expands into its extracted dependency fields.
                    // Fragment spreads / inline fragments expand into their concrete fields, so look through
                    // them too (fragments can nest fragments).
                    RegisterBulkResolverFields(compileContext, field, fragments, docParam, docVariables, replacer);
                }
                // Might be a fragment that expands into many fields hence the Expand
                // or a service field that we expand into the required fields for input
                foreach (var subField in field.Expand(compileContext, fragments, withoutServiceFields, nextFieldContext, docParam, docVariables))
                {
                    try
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
                            compileContext.GetPossibleNextContextTypes(this),
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
                        if (potentialMatch is BaseGraphQLField existingFieldNode)
                            ValidateFieldsCanMerge(existingFieldNode, subField);

                        selectionFields[subField] = new CompiledField(subField, fieldExp);
                    }
                    catch (EntityGraphQLFieldException ex)
                    {
                        throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, $"Field '{Name}' - {ex.Message}", null, BuildPath(), ex);
                    }
                }
            }
        }
        finally
        {
            compileContext.PopSelectionPathNode();
        }

        return selectionFields;
    }

    /// <summary>
    /// Register the bulk resolver for a selected field, looking through fragment spreads and inline
    /// fragments to the concrete fields they select. Every field found this way is selected on this node
    /// (the one compiling its selection set), whatever its own ParentNode says. The bulk list expression
    /// path comes from the compile context's selection path.
    /// </summary>
    private void RegisterBulkResolverFields(
        CompileContext compileContext,
        BaseGraphQLField field,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        ParameterExpression? docParam,
        IArgumentsTracker? docVariables,
        ParameterReplacer replacer
    )
    {
        if (field.Field?.BulkResolver != null && !IsSelectedOnToSingleNode(compileContext))
        {
            HandleBulkResolverForField(compileContext, field, field.Field.BulkResolver, docParam, docVariables, replacer);
        }
        else if (field is GraphQLFragmentSpreadField)
        {
            var fragment = fragments.GetValueOrDefault(field.Name);
            if (fragment != null) // unknown fragment names error later in Expand
            {
                foreach (var fragField in fragment.QueryFields)
                    RegisterBulkResolverFields(compileContext, fragField, fragments, docParam, docVariables, replacer);
            }
        }
        else if (field is GraphQLInlineFragmentField)
        {
            foreach (var fragField in field.QueryFields)
                RegisterBulkResolverFields(compileContext, fragField, fragments, docParam, docVariables, replacer);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void HandleBulkResolverForField(
        CompileContext compileContext,
        BaseGraphQLField field,
        IBulkFieldResolver bulkResolver,
        ParameterExpression? docParam,
        IArgumentsTracker? docVariables,
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
        IArgumentsTracker? docVariables,
        ParameterReplacer replacer
    )
    {
        // Need the args that may be used in the bulk resolver expression
        var argumentValue = default(object);
        var validationErrors = new HashSet<string>();
        var bulkFieldArgParam = bulkResolver.BulkArgParam;
        var newArgParam = bulkFieldArgParam != null ? Expression.Parameter(bulkFieldArgParam!.Type, $"{bulkFieldArgParam.Name}_exec") : null;
        compileContext.AddArgsToCompileContext(field.Field!, field.Arguments, docParam, docVariables, ref argumentValue, validationErrors, newArgParam);

        // replace the arg param after extensions (don't rely on extensions to do this)
        Expression bulkFieldExpr = bulkResolver.FieldExpression;

        GraphQLHelper.ValidateAndReplaceFieldArgs(field.Field!, bulkFieldArgParam, replacer, ref argumentValue, ref bulkFieldExpr, validationErrors, newArgParam);
        // The path of selection nodes from the statement to the field. Taken from the compile context's
        // current selection path rather than walking field.ParentNode - fields selected via a fragment
        // spread have the fragment STATEMENT as their parent chain, not the real selection point.
        List<IGraphQLNode> listExpressionPath = compileContext.SelectionPathSnapshot();
        compileContext.AddBulkResolver(
            bulkResolver.Name,
            bulkResolver.DataSelector,
            (LambdaExpression)bulkFieldExpr,
            bulkResolver.ExtractedFields,
            listExpressionPath,
            bulkResolver.FieldExpression.Parameters.First().Type,
            bulkResolver.IsAsync,
            bulkResolver.MaxConcurrency
        );
        compileContext.AddServices(field.Field!.Services);
    }
}
