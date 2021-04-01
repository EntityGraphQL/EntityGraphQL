using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

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
        public override ExpressionResult GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields = false, ParameterExpression replaceContextWith = null, bool isRoot = false)
        {
            if (ShouldRebuildExpression(withoutServiceFields, replaceContextWith))
            {
                var currentContextParam = SelectionContext != null ? SelectionContext.AsParameter() : RootFieldParameter;
                var listContext = fieldExpression;
                if (replaceContextWith != null)
                {
                    var fieldType = isRoot ? replaceContextWith.Type : replaceContextWith.Type.GetField(Name)?.FieldType;
                    // if null we're in a service returned object and no longer need to replace the parameters
                    if (fieldType != null)
                    {
                        if (fieldType.IsEnumerableOrArray())
                            fieldType = fieldType.GetEnumerableOrArrayType();

                        currentContextParam = Expression.Parameter(fieldType, currentContextParam.Name);
                        listContext = isRoot ? (ExpressionResult)replaceContextWith : (ExpressionResult)replacer.Replace(listContext, RootFieldParameter, replaceContextWith);
                        listContext.AddServices(fieldExpression.Services);
                    }
                }

                var selectionFields = GetSelectionFields(serviceProvider, fragments, withoutServiceFields, replaceContextWith != null ? currentContextParam : null);

                if (selectionFields == null || !selectionFields.Any())
                    return null;

                // build a .Select(...) - returning a IEnumerable<>
                var resultExpression = (ExpressionResult)ExpressionUtil.MakeSelectWithDynamicType(currentContextParam, listContext, selectionFields.ExpressionOnly());

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

            if (fullNodeExpression != null)
            {
                fullNodeExpression.AddServices(this.Services);
                // if selecting final graph make sure lists are evaluated
                if (replaceContextWith != null && !isRoot)
                    fullNodeExpression = ExpressionUtil.MakeCallOnEnumerable("ToList", new Type[] { fullNodeExpression.Type.GetEnumerableOrArrayType() }, fullNodeExpression);
            }

            fullNodeExpression?.AddServices(this.Services);

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
