using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

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
        public Expression ListExpression { get; internal set; }

        /// <summary>
        /// Create a new GraphQLQueryNode. Represents both fields in the query as well as the root level fields on the Query type
        /// </summary>
        /// <param name="schema">The Schema Provider that defines the GraphQL schema</param>
        /// <param name="field">Field from the schema that this GraphQLListSelectionField is built from</param>
        /// <param name="name">Name of the field. Could be the alias that the user provided</param>
        /// <param name="nextFieldContext">A context for a field building on this. This will be the list element parameter</param>
        /// <param name="rootParameter">Root parameter used by this nodeExpression (movie in example above).</param>
        /// <param name="nodeExpression">Expression for the list</param>
        /// <param name="context">Partent node</param>
        /// <param name="arguments"></param>
        public GraphQLListSelectionField(ISchemaProvider schema, IField? field, string name, ParameterExpression? nextFieldContext, ParameterExpression? rootParameter, Expression nodeExpression, IGraphQLNode context, Dictionary<string, object>? arguments)
            : base(schema, field, name, nextFieldContext, rootParameter, context, arguments)
        {
            this.ListExpression = nodeExpression;
        }

        public GraphQLListSelectionField(GraphQLListSelectionField context, ParameterExpression? nextFieldContext)
           : base(context, nextFieldContext)
        {
            this.ListExpression = context.ListExpression;
        }

        /// <summary>
        /// The dotnet Expression for this node. Could be as simple as (Person p) => p.Name
        /// Or as complex as (DbContext ctx) => ctx.People.Where(...).Select(p => new {...}).First()
        /// If there is a object selection (new {} in a Select() or not) we will build the NodeExpression on
        /// Execute() so we can look up any query fragment selections
        /// </summary>
        protected override Expression? GetFieldExpression(CompileContext compileContext, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext, bool isRoot, bool contextChanged, ParameterReplacer replacer)
        {
            if (withoutServiceFields && isRoot && HasServices)
                return Field?.ExtractedFieldsFromServices?.FirstOrDefault()?.FieldExpressions!.First();

            var listContext = ListExpression;
            ParameterExpression? nextFieldContext = (ParameterExpression)NextFieldContext!;
            if (contextChanged && replacementNextFieldContext != null)
            {
                listContext = ReplaceContext(replacementNextFieldContext!, isRoot, replacer, listContext!);
                nextFieldContext = Expression.Parameter(listContext.Type.GetEnumerableOrArrayType()!, $"{nextFieldContext.Name}2");
            }
            (listContext, var argumentParam) = Field?.GetExpression(listContext!, replacementNextFieldContext, ParentNode!, schemaContext, compileContext, Arguments, docParam, docVariables, Directives, contextChanged, replacer) ?? (ListExpression, null);
            if (listContext == null)
                return null;

            (listContext, var newNextFieldContext) = ProcessExtensionsPreSelection(listContext, nextFieldContext, replacer);
            if (newNextFieldContext != null)
                nextFieldContext = newNextFieldContext;

            var selectionFields = GetSelectionFields(compileContext, serviceProvider, fragments, docParam, docVariables, withoutServiceFields, nextFieldContext, schemaContext, contextChanged, replacer);

            if (selectionFields == null || !selectionFields.Any())
            {
                if (withoutServiceFields && HasServices)
                    return Field?.ExtractedFieldsFromServices?.FirstOrDefault()?.FieldExpressions!.First();
                return listContext;
            }

            (listContext, selectionFields, nextFieldContext) = ProcessExtensionsSelection(listContext, selectionFields, nextFieldContext, argumentParam, contextChanged, replacer);

            if (HasServices)
                compileContext.AddServices(Field!.Services);

            if (!withoutServiceFields)
            {
                bool needsServiceWrap = NeedsServiceWrap(withoutServiceFields);
                if (needsServiceWrap)
                {
                    var wrappedExpression = WrapWithNullCheck(compileContext, nextFieldContext!, listContext, selectionFields, serviceProvider, schemaContext, replacer);
                    return wrappedExpression;
                }
            }
            // build a .Select(...) - returning a IEnumerable<>
            var resultExpression = ExpressionUtil.MakeSelectWithDynamicType(this, nextFieldContext!, listContext, selectionFields);

            // if selecting final graph make sure lists are evaluated
            if (!isRoot && !withoutServiceFields && resultExpression.Type.IsEnumerableOrArray() && !resultExpression.Type.IsDictionary())
                resultExpression = ExpressionUtil.MakeCallOnEnumerable("ToList", new[] { resultExpression.Type.GetEnumerableOrArrayType()! }, resultExpression);

            return resultExpression;
        }

        private Expression WrapWithNullCheck(CompileContext compileContext, ParameterExpression selectParam, Expression listContext, Dictionary<IFieldKey, CompiledField> selectExpressions, IServiceProvider? serviceProvider, ParameterExpression schemaContext, ParameterReplacer replacer)
        {
            // null check on listContext which may be a call to a service that we do not want to call twice
            var fieldParamValues = new List<object?>(compileContext.ConstantParameters.Values);
            var fieldParams = new List<ParameterExpression>(compileContext.ConstantParameters.Keys);

            // TODO services injected here - is this needed?
            var updatedListContext = listContext;
            if (compileContext.Services.Any() == true)
            {
                updatedListContext = GraphQLHelper.InjectServices(serviceProvider, compileContext.Services, fieldParamValues, listContext, fieldParams, replacer);

                foreach (var selectExpression in selectExpressions)
                {
                    selectExpression.Value.Expression = GraphQLHelper.InjectServices(serviceProvider, compileContext.Services, fieldParamValues, selectExpression.Value.Expression, fieldParams, replacer);
                }
            }

            // replace with null_wrap
            // this is the parameter used in the null wrap. We pass it to the wrap function which has the value to match
            var nullWrapParam = Expression.Parameter(updatedListContext.Type, "nullwrap");

            var callOnList = ExpressionUtil.MakeSelectWithDynamicType(this, selectParam, nullWrapParam, selectExpressions);

            updatedListContext = ExpressionUtil.WrapListFieldForNullCheck(updatedListContext, callOnList, fieldParams, fieldParamValues, nullWrapParam, schemaContext);
            return updatedListContext;
        }
    }
}
