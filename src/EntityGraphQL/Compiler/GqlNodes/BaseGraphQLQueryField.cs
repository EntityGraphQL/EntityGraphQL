using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Base class for both the ListSelectionField and ObjectProjectionField
    /// </summary>
    public abstract class BaseGraphQLQueryField : BaseGraphQLField
    {
        /// <summary>
        /// The Expression (usually a ParameterExpression or MemberExpression) used to build the Select object
        /// If the field is not IEnumerable e.g. param.Name, this is not used as the selection will be built using param.Name
        /// If the field is IEnumerable e.g. param.People, this will be a ParameterExpression of the element type of People.
        /// </summary>
        protected ExpressionResult selectionContext;
        public ExpressionResult SelectionContext => selectionContext;

        /// <summary>
        /// Holds the node's dotnet ExpressionStatement
        /// </summary>
        protected ExpressionResult fullNodeExpression;
        /// <summary>
        /// Holds the expression without any fields that use services
        /// </summary>
        protected ExpressionResult nodeExpressionNoServiceFields;
        protected bool hasWrappedService;

        /// <summary>
        /// Field is a complex expression (using a method or function) that returns a single object (not IEnumerable)
        /// We wrap this is a function that does a null check and avoid duplicate calls on the method/service
        /// </summary>
        /// <value></value>
        public override bool HasAnyServices { get => hasWrappedService || QueryFields.Any(q => q.HasAnyServices) || Services?.Any() == true || QueryFields.Any(q => q.Services?.Any() == true); set => hasWrappedService = value; }

        protected List<BaseGraphQLField> queryFields;

        protected BaseGraphQLQueryField()
        {
            if (selectionContext != null)
                Services.AddRange(selectionContext.Services);
        }

        /// <summary>
        /// The fields that this node selects
        /// query {
        ///     rootField { queryField1 queryField2 ... }
        // }
        /// </summary>
        public IEnumerable<BaseGraphQLField> QueryFields { get => queryFields; }



        protected bool ShouldRebuildExpression(bool withoutServiceFields, ParameterExpression buildServiceWrapWithParam)
        {
            return (nodeExpressionNoServiceFields == null && withoutServiceFields) ||
                                        (buildServiceWrapWithParam != null && fullNodeExpression != null) ||
                                        (fullNodeExpression == null && queryFields.Any());
        }
        protected Dictionary<string, CompiledField> GetSelectionFields(object contextValue, IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, ParameterExpression buildServiceWrapWithParam)
        {
            // do we have services at this level
            if (withoutServiceFields && Services.Any())
                return null;

            var selectionFields = new Dictionary<string, CompiledField>();
            var replacer = new ParameterReplacer();

            foreach (var field in queryFields)
            {
                // Might be a fragment that expands into many fields hence the Expand
                foreach (var subField in field.Expand(fragments))
                {
                    if (withoutServiceFields && subField.HasAnyServices)
                        continue;

                    var fieldExp = subField.GetNodeExpression(contextValue, serviceProvider, fragments, withoutServiceFields, buildServiceWrapWithParam);

                    // be nice not to have to handle this here...
                    if (SelectionContext != null && field is GraphQLFragmentField fragField)
                    {
                        fieldExp.Expression = replacer.Replace(fieldExp.Expression, fragField.Fragment.SelectContext, SelectionContext);
                    }
                    selectionFields[subField.Name] = new CompiledField(subField, fieldExp);

                    // pull any constant values up
                    foreach (var item in subField.ConstantParameters)
                    {
                        if (!constantParameters.ContainsKey(item.Key))
                            constantParameters.Add(item.Key, item.Value);
                    }
                    // pull up any services
                    AddServices(fieldExp.Services);
                }
            }

            return selectionFields;
        }

        public void AddServices(IEnumerable<Type> services)
        {
            if (services == null)
                return;
            Services.AddRange(services);
        }

        public void SetNodeExpression(ExpressionResult expr)
        {
            fullNodeExpression = expr;
        }
    }
}