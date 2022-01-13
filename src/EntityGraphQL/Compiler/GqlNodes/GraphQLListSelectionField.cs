using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.FieldExtensions;

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
        private readonly ExpressionExtractor extractor;
        public Expression ListExpression { get; internal set; }

        /// <summary>
        /// Create a new GraphQLQueryNode. Represents both fields in the query as well as the root level fields on the Query type
        /// </summary>
        /// <param name="fieldExtensions">Any field extensions to apply to the expressions</param>
        /// <param name="name">Name of the field. Could be the alias that the user provided</param>
        /// <param name="nextFieldContext">A context for a field building on this. This will be the list element parameter</param>
        /// <param name="rootParameter">Root parameter used by this nodeExpression (movie in example above).</param>
        /// <param name="nodeExpression">Expression for the list</param>
        /// <param name="context">Partent node</param>
        public GraphQLListSelectionField(IEnumerable<IFieldExtension> fieldExtensions, string name, ParameterExpression nextFieldContext, ParameterExpression rootParameter, Expression nodeExpression, IGraphQLNode context)
            : base(name, nextFieldContext, rootParameter, context)
        {
            this.fieldExtensions = fieldExtensions?.ToList();
            this.ListExpression = nodeExpression;
            constantParameters = new Dictionary<ParameterExpression, object>();
            extractor = new ExpressionExtractor();
        }

        /// <summary>
        /// The dotnet Expression for this node. Could be as simple as (Person p) => p.Name
        /// Or as complex as (DbContext ctx) => ctx.People.Where(...).Select(p => new {...}).First()
        /// If there is a object selection (new {} in a Select() or not) we will build the NodeExpression on
        /// Execute() so we can look up any query fragment selections
        /// </summary>
        public override Expression GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression schemaContext, bool withoutServiceFields, Expression replacementNextFieldContext = null, bool isRoot = false, bool contextChanged = false)
        {
            ParameterExpression nextFieldContext = (ParameterExpression)NextFieldContext;
            Expression listContext = ListExpression;
            if (contextChanged && Name != "__typename")
            {
                var possibleField = replacementNextFieldContext.Type.GetField(Name);
                if (possibleField != null)
                    listContext = Expression.Field(replacementNextFieldContext, possibleField);
                else
                    listContext = isRoot ? replacementNextFieldContext : replacer.ReplaceByType(listContext, ParentNode.NextFieldContext.Type, replacementNextFieldContext);
                nextFieldContext = Expression.Parameter(listContext.Type.GetEnumerableOrArrayType(), $"{nextFieldContext.Name}2");
            }

            (listContext, nextFieldContext) = ProcessExtensionsPreSelection(GraphQLFieldType.ListSelection, listContext, nextFieldContext, replacer);

            var selectionFields = GetSelectionFields(serviceProvider, fragments, withoutServiceFields, nextFieldContext, schemaContext, contextChanged);

            if (selectionFields == null || !selectionFields.Any())
            {
                if (withoutServiceFields && Services.Any())
                    return null;
                return listContext;
            }

            (listContext, selectionFields, nextFieldContext) = ProcessExtensionsSelection(GraphQLFieldType.ListSelection, listContext, selectionFields, nextFieldContext, replacer);
            // build a .Select(...) - returning a IEnumerable<>
            var resultExpression = ExpressionUtil.MakeSelectWithDynamicType(nextFieldContext, listContext, selectionFields.ExpressionOnly());

            if (!withoutServiceFields)
            {
                // if selecting final graph make sure lists are evaluated
                if (!isRoot && resultExpression.Type.IsEnumerableOrArray())
                    resultExpression = ExpressionUtil.MakeCallOnEnumerable("ToList", new Type[] { resultExpression.Type.GetEnumerableOrArrayType() }, resultExpression);
            }

            return resultExpression;
        }

        protected override Dictionary<string, CompiledField> GetSelectionFields(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, Expression nextFieldContext, ParameterExpression schemaContext, bool contextChanged)
        {
            var fields = base.GetSelectionFields(serviceProvider, fragments, withoutServiceFields, nextFieldContext, schemaContext, contextChanged);

            // extract possible fields from listContext (might be .Where(), OrderBy() etc)
            if (withoutServiceFields && fields != null)
            {
                var extractedFields = extractor.Extract(ListExpression, (ParameterExpression)nextFieldContext, true);
                if (extractedFields != null)
                    extractedFields.ToDictionary(i => i.Key, i =>
                    {
                        var replaced = replacer.ReplaceByType(i.Value, nextFieldContext.Type, nextFieldContext);
                        return new CompiledField(new GraphQLScalarField(null, i.Key, replaced, RootParameter, this), replaced);
                    })
                    .ToList()
                    .ForEach(i =>
                    {
                        if (!fields.ContainsKey(i.Key))
                            fields.Add(i.Key, i.Value);
                    });
            }

            return fields;
        }
    }
}
