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
        protected BaseGraphQLQueryField(ISchemaProvider schema, IField? field, string name, Expression? nextFieldContext, ParameterExpression? rootParameter, IGraphQLNode? parentNode, Dictionary<string, object>? arguments)
            : base(schema, field, name, nextFieldContext, rootParameter, parentNode, arguments)
        {
        }

        protected BaseGraphQLQueryField(BaseGraphQLQueryField context, Expression? nextFieldContext)
            : base(context, nextFieldContext)
        {
        }

        public override IEnumerable<BaseGraphQLField> Expand(CompileContext compileContext, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, Expression fieldContext, ParameterExpression? docParam, object? docVariables)
        {
            var result = ProcessFieldDirectives(this, docParam, docVariables);
            if (result == null)
                return new List<BaseGraphQLField>();

            return new List<BaseGraphQLField> { this };
        }

        protected bool NeedsServiceWrap(bool withoutServiceFields) => !withoutServiceFields && Field?.Services.Any() == true;

        protected void ExtractRequiredFieldsForPreServiceRun(Expression extractFrom, string fieldName, Expression nextFieldContext, ParameterReplacer replacer, Dictionary<string, CompiledField> fields)
        {
            var extractor = new ExpressionExtractor();
            var extractedFields = extractor.Extract(extractFrom, nextFieldContext, true, fieldName);
            if (extractedFields != null)
                extractedFields.ToDictionary(i => i.Key, i =>
                {
                    var replaced = replacer.ReplaceByType(i.Value, nextFieldContext.Type, nextFieldContext);
                    return new CompiledField(new GraphQLExtractedField(schema, i.Key, replaced, nextFieldContext), replaced);
                })
                .ToList()
                .ForEach(i =>
                {
                    if (!fields.ContainsKey(i.Key))
                        fields.Add(i.Key, i.Value);
                });
        }

        protected virtual Dictionary<string, CompiledField> GetSelectionFields(CompileContext compileContext, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, bool withoutServiceFields, Expression nextFieldContext, ParameterExpression schemaContext, bool contextChanged, ParameterReplacer replacer)
        {
            var selectionFields = new Dictionary<string, CompiledField>(StringComparer.OrdinalIgnoreCase);

            foreach (var field in QueryFields)
            {
                // Might be a fragment that expands into many fields hence the Expand
                // or a service field that we expand into the required fields for input
                foreach (var subField in field.Expand(compileContext, fragments, withoutServiceFields, nextFieldContext, docParam, docVariables))
                {
                    var fieldExp = subField.GetNodeExpression(compileContext, serviceProvider, fragments, docParam, docVariables, schemaContext, withoutServiceFields, nextFieldContext, false, contextChanged, replacer);
                    if (fieldExp == null)
                        continue;

                    // do we have services at this level
                    if (withoutServiceFields && (subField.HasServices || Field?.Services.Any() == true))
                        ExtractRequiredFieldsForPreServiceRun(fieldExp, subField.Name, nextFieldContext!, replacer, selectionFields);
                    else
                    {
                        // if this came from a fragment we need to fix the expression context
                        if (nextFieldContext != null && field is GraphQLFragmentField)
                        {
                            fieldExp = replacer.Replace(fieldExp, subField.RootParameter!, nextFieldContext);
                        }

                        selectionFields[subField.Name] = new CompiledField(subField, fieldExp);
                    }
                }
            }

            return selectionFields.Count == 0 ? new Dictionary<string, CompiledField>() : selectionFields;
        }
    }
}