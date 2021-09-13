using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Represents a field node in the GraphQL query. That operates on a single object.
    /// query MyQuery {
    ///     people {
    ///         id, name
    ///     }
    ///     customer { # GraphQLObjectProjectionField
    ///         id
    ///     }
    /// }
    ///
    /// Builds an expression like
    /// ctx => new { Id = ctx.Customer.Id }
    /// </summary>
    public class GraphQLObjectProjectionField : BaseGraphQLQueryField
    {
        private readonly ExpressionExtractor extractor;

        /// <summary>
        /// Create a new GraphQLQueryNode. Represents both fields in the query as well as the root level fields on the Query type
        /// </summary>
        /// <param name="extensions">Field extensions to apply to the expressions</param>
        /// <param name="name">Name of the field</param>
        /// <param name="nextContextExpression">The next context expression for ObjectProjection is also our field expression e..g person.manager</param>
        /// <param name="rootParameter">The root parameter</param>
        /// <param name="parentNode"></param>
        public GraphQLObjectProjectionField(List<IFieldExtension> extensions, string name, Expression nextContextExpression, ParameterExpression rootParameter, IGraphQLNode parentNode)
            : base(name, nextContextExpression, rootParameter, parentNode)
        {
            this.fieldExtensions = extensions;
            extractor = new ExpressionExtractor();
        }

        /// <summary>
        /// The dotnet Expression for this node. Could be as simple as (Person p) => p.Name
        /// Or as complex as (DbContext ctx) => ctx.People.Where(...).Select(p => new {...}).First()
        /// If there is a object selection (new {} in a Select() or not) we will build the NodeExpression on
        /// Execute() so we can look up any query fragment selections
        /// </summary>
        public override Expression GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression schemaContext, bool withoutServiceFields, Expression replaceContextWith = null, bool isRoot = false, bool useReplaceContextDirectly = false)
        {
            bool needsServiceWrap = !useReplaceContextDirectly && !withoutServiceFields && HasAnyServices(fragments);

            var resultExpression = NextContextExpression;

            if (needsServiceWrap ||
                NextContextExpression.NodeType == ExpressionType.MemberInit || NextContextExpression.NodeType == ExpressionType.New)
            {
                var updatedExpression = WrapWithNullCheck(serviceProvider, fragments, withoutServiceFields, replaceContextWith, useReplaceContextDirectly, schemaContext);
                resultExpression = updatedExpression;
            }
            else
            {
                if (replaceContextWith != null)
                {
                    if (useReplaceContextDirectly)
                    {
                        resultExpression = replaceContextWith;
                    }
                    else
                    {
                        // pre services select has created the field Name in the new context
                        // although this may not have had a pre services select done as we use replaceContextWith for selection contet too
                        resultExpression = Expression.PropertyOrField(replaceContextWith, Name);
                    }
                }

                // don't replace context is needsServiceWrap as the selection fields happen internally to the wrap call on the coorect context
                var selectionFields = GetSelectionFields(serviceProvider, fragments, withoutServiceFields, replaceContextWith != null ? resultExpression : null, schemaContext);

                if (selectionFields == null || !selectionFields.Any())
                    return null;

                (resultExpression, selectionFields, _) = ProcessExtensionsSelection(GraphQLFieldType.ObjectProjection, resultExpression, selectionFields, null, replacer);
                // build a new {...} - returning a single object {}
                var newExp = ExpressionUtil.CreateNewExpression(selectionFields.ExpressionOnly(), out Type anonType);
                if (resultExpression.NodeType != ExpressionType.MemberInit && resultExpression.NodeType != ExpressionType.New)
                {
                    if (selectionFields.Any() && !withoutServiceFields)
                    {
                        // make a null check from this new expression
                        newExp = Expression.Condition(Expression.MakeBinary(ExpressionType.Equal, resultExpression, Expression.Constant(null)), Expression.Constant(null, anonType), newExp, anonType);
                        resultExpression = newExp;
                    }
                    else
                    {
                        resultExpression = Expression.Condition(Expression.MakeBinary(ExpressionType.Equal, resultExpression, Expression.Constant(null)), Expression.Constant(null, anonType), newExp, anonType);
                    }
                }
                else
                {
                    if (selectionFields.Any() && !withoutServiceFields)
                        resultExpression = newExp;
                    else
                        resultExpression = newExp;
                }
            }

            return resultExpression;
        }

        /// <summary>
        /// These expression will be built on the element type
        /// we might be using a service i.e. ctx => WithService((T r) => r.DoSomething(ctx.Entities.Select(f => f.Id).ToList()))
        /// if we can we want to avoid calling that multiple times with a expression like
        /// r.DoSomething(ctx.Entities.Select(f => f.Id).ToList()) == null ? null : new {
        ///      Field = r.DoSomething(ctx.Entities.Select(f => f.Id).ToList()).Blah
        /// }
        /// by wrapping the whole thing in a method that does the null check once.
        /// This means we build the fieldExpressions on a parameter of the result type
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="fragments"></param>
        /// <param name="withoutServiceFields"></param>
        /// <param name="replaceContextWith"></param>
        /// <param name="useReplaceContextDirectly"></param>
        /// <param name="schemaContext"></param>
        /// <returns></returns>
        private Expression WrapWithNullCheck(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, Expression replaceContextWith, bool useReplaceContextDirectly, ParameterExpression schemaContext)
        {
            // don't replace context is needsServiceWrap as the selection fields happen internally to the wrap call on the correct context
            var selectionFields = GetSelectionFields(serviceProvider, fragments, withoutServiceFields, null, schemaContext);

            if (selectionFields == null || !selectionFields.Any())
                return null;

            // selectionFields is set up but we need to wrap
            // we wrap here as we have access to the values and services etc
            var fieldParamValues = new List<object>(ConstantParameters.Values);
            var fieldParams = new List<ParameterExpression>(ConstantParameters.Keys);

            var updatedExpression = Services.Any() ? GraphQLHelper.InjectServices(serviceProvider, Services, fieldParamValues, NextContextExpression, fieldParams, replacer) : NextContextExpression;
            // replace with null_wrap
            var nullWrapParam = Expression.Parameter(updatedExpression.Type, "nullwrap");
            // This is the var the we use in the select - the result of the service at runtime
            var selectionParams = new List<ParameterExpression>();
            selectionParams.AddRange(fieldParams);
            var selectionParamValues = new List<object>();
            selectionParamValues.AddRange(fieldParamValues);

            if (replaceContextWith != null)
            {
                if (useReplaceContextDirectly)
                    updatedExpression = replaceContextWith;
                else
                    updatedExpression = replacer.ReplaceByType(updatedExpression, ParentNode.NextContextExpression.Type, replaceContextWith);

                nullWrapParam = Expression.Parameter(updatedExpression.Type, "nullwrap");

                foreach (var item in selectionFields)
                {
                    if (item.Value.Field.Services.Any())
                        item.Value.Expression = replacer.ReplaceByType(item.Value.Expression, NextContextExpression.Type, nullWrapParam);
                    else if (item.Key != "__typename") // this is a static selection of the type name
                                                       // pre service selection has selected the fields as the names we expect already
                        item.Value.Expression = Expression.PropertyOrField(nullWrapParam, item.Key);
                }
            }
            else
            {
                foreach (var item in selectionFields)
                {
                    item.Value.Expression = replacer.ReplaceByType(item.Value.Expression, NextContextExpression.Type, nullWrapParam);
                }
            }

            (updatedExpression, selectionFields, _) = ProcessExtensionsSelection(GraphQLFieldType.ObjectProjection, updatedExpression, selectionFields, null, replacer);
            // we need to make sure the wrap can resolve any services in the select
            var selectionExpressions = selectionFields.ToDictionary(f => f.Key, f => GraphQLHelper.InjectServices(serviceProvider, f.Value.Field.Services, fieldParamValues, f.Value.Expression, fieldParams, replacer));

            updatedExpression = ExpressionUtil.WrapFieldForNullCheck(updatedExpression, selectionParams, selectionExpressions, selectionParamValues, nullWrapParam, schemaContext);
            return updatedExpression;
        }

        public override IEnumerable<BaseGraphQLField> Expand(List<GraphQLFragmentStatement> fragments, bool withoutServiceFields)
        {
            // fieldExpression might be a service method call and the arguments might have fields we need to select out
            if (withoutServiceFields && NextContextExpression.NodeType == ExpressionType.Call)
            {
                IDictionary<string, Expression> fieldsRequiredForServices = extractor.Extract(NextContextExpression, RootParameter);
                if (fieldsRequiredForServices != null)
                {
                    var fields = fieldsRequiredForServices
                        .Select(i => new GraphQLScalarField(null, i.Key, i.Value, RootParameter, ParentNode))
                        .ToList();

                    if (fields.Any())
                        return fields;
                }
            }
            return base.Expand(fragments, withoutServiceFields);
        }
    }
}
