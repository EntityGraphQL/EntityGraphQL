using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
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
        protected List<Type>? PossibleNextContextTypes { get; set; }

        protected BaseGraphQLQueryField(ISchemaProvider schema, IField? field, string name, Expression? nextFieldContext, ParameterExpression? rootParameter, IGraphQLNode? parentNode, IReadOnlyDictionary<string, object>? arguments)
            : base(schema, field, name, nextFieldContext, rootParameter, parentNode, arguments)
        {
        }

        protected BaseGraphQLQueryField(BaseGraphQLQueryField context, Expression? nextFieldContext)
            : base(context, nextFieldContext)
        {
        }

        protected bool NeedsServiceWrap(bool withoutServiceFields) => !withoutServiceFields && HasServices;

        protected virtual Dictionary<IFieldKey, CompiledField> GetSelectionFields(CompileContext compileContext, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, bool withoutServiceFields, Expression nextFieldContext, ParameterExpression schemaContext, bool contextChanged, ParameterReplacer replacer)
        {
            var selectionFields = new Dictionary<IFieldKey, CompiledField>();

            foreach (var field in QueryFields)
            {
                if (field.Field?.BulkResolver != null)
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
                    if (!contextChanged && subField.RootParameter != null && actualNextFieldContext.Type != subField.RootParameter.Type && (field is GraphQLInlineFragmentField || field is GraphQLFragmentSpreadField) && (subField.FromType?.BaseTypesReadOnly.Any() == true || Field?.ReturnType.SchemaType.GqlType == GqlTypes.Union))
                    {
                        actualNextFieldContext = subField.RootParameter;
                    }

                    var fieldExp = subField.GetNodeExpression(compileContext, serviceProvider, fragments, docParam, docVariables, schemaContext, withoutServiceFields, actualNextFieldContext, PossibleNextContextTypes, false, contextChanged, replacer);
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

        protected virtual ParameterExpression? HandleBulkResolverForField(CompileContext compileContext, BaseGraphQLField field, IBulkFieldResolver bulkResolver, ParameterExpression? docParam, object? docVariables, ParameterReplacer replacer)
        {
            // default do nothing
            return field.Field?.ArgumentsParameter;
        }
    }
}