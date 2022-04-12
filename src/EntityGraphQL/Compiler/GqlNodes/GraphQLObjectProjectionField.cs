using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
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
        /// <summary>
        /// Create a new GraphQLQueryNode. Represents both fields in the query as well as the root level fields on the Query type
        /// </summary>
        /// <param name="extensions">Field extensions to apply to the expressions</param>
        /// <param name="name">Name of the field</param>
        /// <param name="nextFieldContext">The next context expression for ObjectProjection is also our field expression e..g person.manager</param>
        /// <param name="rootParameter">The root parameter</param>
        /// <param name="parentNode"></param>
        public GraphQLObjectProjectionField(IField field, List<IFieldExtension> extensions, string name, Expression nextFieldContext, ParameterExpression rootParameter, IGraphQLNode parentNode, Dictionary<string, object>? arguments)
            : base(name, nextFieldContext, rootParameter, parentNode, arguments)
        {
            this.fieldExtensions = extensions;
            this.field = field;
            this.AddServices(field.Services);
        }

        /// <summary>
        /// The dotnet Expression for this node. Could be as simple as (Person p) => p.Name
        /// Or as complex as (DbContext ctx) => ctx.People.Where(...).Select(p => new {...}).First()
        /// If there is a object selection (new {} in a Select() or not) we will build the NodeExpression on
        /// Execute() so we can look up any query fragment selections
        /// </summary>
        public override Expression? GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, Dictionary<string, object> parentArguments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext = null, bool isRoot = false, bool contextChanged = false)
        {
            var nextFieldContext = NextFieldContext;

            if (contextChanged && Name != "__typename" && replacementNextFieldContext != null)
            {
                var possibleField = replacementNextFieldContext?.Type.GetField(Name);
                if (possibleField != null)
                    nextFieldContext = Expression.Field(replacementNextFieldContext, possibleField);
                else
                    nextFieldContext = isRoot ? replacementNextFieldContext : replacer.ReplaceByType(nextFieldContext!, ParentNode!.NextFieldContext!.Type, replacementNextFieldContext!);
            }
            var nextFieldContextExp = field!.GetExpression(nextFieldContext!, replacementNextFieldContext, ParentNode!, schemaContext, parentArguments.MergeNew(arguments), docParam, docVariables, directives, contextChanged);
            if (nextFieldContextExp == null)
                return null;
            nextFieldContext = nextFieldContextExp.Expression;
            AddServices(nextFieldContextExp.Services);
            AddConstantParameters(nextFieldContextExp.ConstantParameters);
            bool needsServiceWrap = !withoutServiceFields && HasAnyServices(fragments);


            (nextFieldContext, _) = ProcessExtensionsPreSelection(GraphQLFieldType.ObjectProjection, nextFieldContext!, null, replacer);

            var selectionFields = GetSelectionFields(serviceProvider, fragments, docParam, docVariables, withoutServiceFields, nextFieldContext, schemaContext, contextChanged);
            if (selectionFields == null || !selectionFields.Any())
                return null;

            if (needsServiceWrap ||
                ((nextFieldContext.NodeType == ExpressionType.MemberInit || nextFieldContext.NodeType == ExpressionType.New) && isRoot))
            {
                nextFieldContext = WrapWithNullCheck(selectionFields, serviceProvider, nextFieldContext, schemaContext, contextChanged);
            }
            else
            {
                (nextFieldContext, selectionFields, _) = ProcessExtensionsSelection(GraphQLFieldType.ObjectProjection, nextFieldContext, selectionFields, null, contextChanged, replacer);
                // build a new {...} - returning a single object {}
                var newExp = ExpressionUtil.CreateNewExpression(selectionFields.ExpressionOnly(), out Type anonType);
                if (nextFieldContext.NodeType != ExpressionType.MemberInit && nextFieldContext.NodeType != ExpressionType.New)
                {
                    // make a null check from this new expression
                    nextFieldContext = Expression.Condition(Expression.MakeBinary(ExpressionType.Equal, nextFieldContext, Expression.Constant(null)), Expression.Constant(null, anonType), newExp, anonType);
                }
                else
                {
                    nextFieldContext = newExp;
                }
            }

            return nextFieldContext;
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
        /// <param name="replacementNextFieldContext"></param>
        /// <param name="schemaContext"></param>
        /// <returns></returns>
        private Expression WrapWithNullCheck(Dictionary<string, CompiledField> selectionFields, IServiceProvider serviceProvider, Expression nextFieldContext, ParameterExpression schemaContext, bool contextChanged)
        {
            // selectionFields is set up but we need to wrap
            // we wrap here as we have access to the values and services etc
            var fieldParamValues = new List<object>(ConstantParameters.Values);
            var fieldParams = new List<ParameterExpression>(ConstantParameters.Keys);

            var updatedExpression = Services.Any() ? GraphQLHelper.InjectServices(serviceProvider, Services, fieldParamValues, nextFieldContext, fieldParams, replacer) : nextFieldContext;
            // replace with null_wrap
            // this is the parameter used in the null wrap. We pass it to the wrap function which has the value to match
            var nullWrapParam = Expression.Parameter(updatedExpression.Type, "nullwrap");
            var selectionParamValues = new List<object>();
            selectionParamValues.AddRange(fieldParamValues);

            if (contextChanged)
            {
                foreach (var item in selectionFields)
                {
                    if (item.Value.Field.Services.Any())
                        item.Value.Expression = replacer.ReplaceByType(item.Value.Expression, nextFieldContext.Type, nullWrapParam);
                    else if (item.Key != "__typename") // this is a static selection of the type name
                                                       // pre service selection has selected the fields as the names we expect already
                        item.Value.Expression = Expression.PropertyOrField(nullWrapParam, item.Key);
                }
            }
            else
            {
                foreach (var item in selectionFields)
                {
                    item.Value.Expression = replacer.ReplaceByType(item.Value.Expression, nextFieldContext.Type, nullWrapParam);
                }
            }

            (updatedExpression, selectionFields, _) = ProcessExtensionsSelection(GraphQLFieldType.ObjectProjection, updatedExpression, selectionFields, null, contextChanged, replacer);
            // we need to make sure the wrap can resolve any services in the select
            var selectionExpressions = selectionFields.ToDictionary(f => f.Key, f => GraphQLHelper.InjectServices(serviceProvider, f.Value.Field.Services, fieldParamValues, f.Value.Expression, fieldParams, replacer));

            updatedExpression = ExpressionUtil.WrapFieldForNullCheck(updatedExpression, fieldParams, selectionExpressions, selectionParamValues, nullWrapParam, schemaContext);
            return updatedExpression;
        }

        public override IEnumerable<BaseGraphQLField> Expand(List<GraphQLFragmentStatement> fragments, bool withoutServiceFields)
        {
            // fieldExpression might be a service method call and the arguments might have fields we need to select out
            if (withoutServiceFields && NextFieldContext?.NodeType == ExpressionType.Call)
            {
                var extractor = new ExpressionExtractor();
                var fieldsRequiredForServices = extractor.Extract(NextFieldContext, ParentNode!.NextFieldContext!, true);
                if (fieldsRequiredForServices != null)
                {
                    var fields = fieldsRequiredForServices
                        .Select(i => new GraphQLScalarField(field, null, i.Key, i.Value, RootParameter, ParentNode, arguments)
                        {
                            // do not push services into the fields extracted from a service field
                            Services = new List<Type>()
                        })
                        .ToList();

                    if (fields.Any())
                        return fields;
                }
            }
            return base.Expand(fragments, withoutServiceFields);
        }
    }
}
