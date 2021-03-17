using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Represents a field node in the GraphQL query. That operates on a list of things.
    /// query MyQuery {
    ///     people { # GraphQLListSelectionField
    ///         id, name
    ///     }
    ///     person(id: "") { id }
    /// }
    /// </summary>
    public class GraphQLListSelectionField : BaseGraphQLQueryField
    {
        private readonly ExpressionResult fieldExpression;

        /// <summary>
        /// Create a new GraphQLQueryNode. Represents both fields in the query as well as the root level fields on the Query type
        /// </summary>
        /// <param name="schemaProvider">The schema provider used to build the expressions</param>
        /// <param name="name">Name of the field. Could be the alias that the user provided</param>
        /// <param name="fieldExpression">The expression that makes the field. e.g. movie => movie.Name</param>
        /// <param name="fieldParameter">The ParameterExpression used for the field expression if required.</param>
        /// <param name="fieldSelection">Any fields that will be selected from this field e.g. (in GQL) { thisField { fieldSelection1 fieldSelection2 } }</param>
        /// <param name="selectionContext">The Expression used to build the fieldSelection expressions</param>
        public GraphQLListSelectionField(string name, ExpressionResult fieldExpression, ParameterExpression fieldParameter, IEnumerable<BaseGraphQLField> fieldSelection, ExpressionResult selectionContext)
        {
            Name = name;
            this.fieldExpression = fieldExpression;
            queryFields = fieldSelection?.ToList() ?? new List<BaseGraphQLField>();
            this.selectionContext = selectionContext;
            this.RootFieldParameter = fieldParameter;
            constantParameters = new Dictionary<ParameterExpression, object>();
            if (fieldExpression != null)
            {
                AddServices(fieldExpression.Services);
                foreach (var item in fieldExpression.ConstantParameters)
                {
                    constantParameters.Add(item.Key, item.Value);
                }
            }
            if (fieldSelection != null)
            {
                AddServices(fieldSelection.SelectMany(s => s.GetType() == typeof(GraphQLListSelectionField) ? ((GraphQLListSelectionField)s).Services : new List<Type>()));
                foreach (var item in fieldSelection.SelectMany(fs => fs.ConstantParameters))
                {
                    constantParameters.Add(item.Key, item.Value);
                }
            }
        }

        /// <summary>
        /// The dotnet Expression for this node. Could be as simple as (Person p) => p.Name
        /// Or as complex as (DbContext ctx) => ctx.People.Where(...).Select(p => new {...}).First()
        /// If there is a object selection (new {} in a Select() or not) we will build the NodeExpression on
        /// Execute() so we can look up any query fragment selections
        /// </summary>
        public override ExpressionResult GetNodeExpression(object contextValue, IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields = false, ParameterExpression buildServiceWrapWithParam = null)
        {
            if (ShouldRebuildExpression(withoutServiceFields, buildServiceWrapWithParam))
            {
                var selectionFields = GetSelectionFields(contextValue, serviceProvider, fragments, withoutServiceFields, buildServiceWrapWithParam);

                if (!selectionFields.Any())
                    return null;

                // build a .Select(...) - returning a IEnumerable<>
                var resultExpression = (ExpressionResult)ExpressionUtil.MakeSelectWithDynamicType(SelectionContext != null ? SelectionContext.AsParameter() : RootFieldParameter, fieldExpression, selectionFields.ExpressionOnly());

                if (combineExpression != null)
                {
                    var exp = (ExpressionResult)ExpressionUtil.CombineExpressions(resultExpression, combineExpression);
                    exp.AddConstantParameters(resultExpression.ConstantParameters);
                    exp.AddServices(resultExpression.Services);
                    resultExpression = exp;
                }
                Services.AddRange(resultExpression?.Services);

                if (withoutServiceFields)
                    nodeExpressionNoServiceFields = resultExpression;
                else
                    fullNodeExpression = resultExpression;
            }

            // above has built the expressions
            if (withoutServiceFields)
                return nodeExpressionNoServiceFields ?? fieldExpression;

            if (fullNodeExpression != null && queryFields != null && queryFields.Any())
                return fullNodeExpression;

            return fieldExpression;
        }

        public override string ToString()
        {
            return $"Node - Name={Name}, Expression={fullNodeExpression.ToString() ?? "not built yet"}";
        }
    }
}
