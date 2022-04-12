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
        protected readonly ParameterReplacer replacer;

        protected BaseGraphQLQueryField(ISchemaProvider schema, string name, Expression? nextFieldContext, ParameterExpression? rootParameter, IGraphQLNode? parentNode, Dictionary<string, object>? arguments)
            : base(schema, name, nextFieldContext, rootParameter, parentNode, arguments)
        {
            replacer = new ParameterReplacer();
        }

        public override IEnumerable<BaseGraphQLField> Expand(List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, ParameterExpression? docParam, object? docVariables)
        {
            var result = (BaseGraphQLQueryField)ProcessFieldDirectives(this, docParam, docVariables);
            if (result == null)
                return new List<BaseGraphQLField>();

            return new List<BaseGraphQLField> { this };
        }

        protected virtual Dictionary<string, CompiledField>? GetSelectionFields(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, bool withoutServiceFields, Expression nextFieldContext, ParameterExpression schemaContext, bool contextChanged)
        {
            // do we have services at this level
            if (withoutServiceFields && Services.Any())
                return null;

            var selectionFields = new Dictionary<string, CompiledField>(StringComparer.OrdinalIgnoreCase);

            foreach (var field in QueryFields)
            {
                // Might be a fragment that expands into many fields hence the Expand
                // or a service field that we expand into the required fields for input
                foreach (var subField in field.Expand(fragments, withoutServiceFields, docParam, docVariables))
                {
                    var fieldExp = subField.GetNodeExpression(serviceProvider, fragments, arguments, docParam, docVariables, schemaContext, withoutServiceFields, nextFieldContext, contextChanged: contextChanged);
                    if (fieldExp == null)
                        continue;

                    // if this came from a fragment we need to fix the expression context
                    if (nextFieldContext != null && field is GraphQLFragmentField)
                    {
                        fieldExp = replacer.Replace(fieldExp, subField.RootParameter!, nextFieldContext);
                    }

                    selectionFields[subField.Name] = new CompiledField(subField, fieldExp);

                    // pull any constant values up
                    foreach (var item in subField.ConstantParameters)
                    {
                        if (!constantParameters.ContainsKey(item.Key))
                            constantParameters.Add(item.Key, item.Value);
                    }
                    Services.AddRange(subField.Services);
                }
            }

            return selectionFields.Count == 0 ? null : selectionFields;
        }
    }
}