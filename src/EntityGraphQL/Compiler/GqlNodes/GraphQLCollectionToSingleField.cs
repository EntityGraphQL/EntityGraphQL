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
    /// Represents a node that has a collection in it's expression path but results in a single entity.
    /// Eg.
    ///     (ctx, id) => ctx.Movies.FirstOrDefault(m => m.Id == id)
    /// Now if the GQL query selects fields from that
    /// {
    ///     movie(id: 1) { id name }
    /// }
    /// To help EF when used (optionally) we can actually build the full expression like
    ///     (ctx, id) => ctx.Movies.Where(m => m.Id == id).Select(m =>
    ///         new {
    ///             id = m.Id,
    ///             name = m.Name
    ///         }).FirstOrDefault()
    ///
    /// Instead of
    ///     (ctx, id) =>
    ///         new {
    ///             id = ctx.Movies.FirstOrDefault(m => m.Id == id)?.Id
    ///             name = ctx.Movies.FirstOrDefault(m => m.Id == id)?.Name,
    ///         }
    /// </summary>
    public class GraphQLCollectionToSingleField : BaseGraphQLQueryField
    {
        private bool isRootField;
        public GraphQLListSelectionField CollectionSelectionNode { get; set; }
        public GraphQLObjectProjectionField ObjectProjectionNode { get; set; }
        public Expression CombineExpression { get; set; }
        public override bool IsRootField
        {
            get => isRootField;
            set
            {
                isRootField = value;
                CollectionSelectionNode.IsRootField = value;
                ObjectProjectionNode.IsRootField = value;
            }
        }

        public GraphQLCollectionToSingleField(ISchemaProvider schema, GraphQLListSelectionField collectionNode, GraphQLObjectProjectionField objectProjectionNode, Expression combineExpression)
            : base(schema, collectionNode.Field, objectProjectionNode.Name, objectProjectionNode.NextFieldContext, objectProjectionNode.RootParameter, objectProjectionNode.ParentNode, null)
        {
            CollectionSelectionNode = collectionNode;
            // do not call ToList as we end up calling First()/etc
            CollectionSelectionNode.AllowToList = false;
            // we need a way to get back to this object in the hierarchy. Might revisit this later
            CollectionSelectionNode.ToSingleNode = this;
            CollectionSelectionNode.IsRootField = IsRootField;
            ObjectProjectionNode = objectProjectionNode;
            ObjectProjectionNode.ToSingleNode = this;
            ObjectProjectionNode.IsRootField = IsRootField;
            CombineExpression = combineExpression;
        }

        public override bool HasServicesAtOrBelow(IEnumerable<GraphQLFragmentStatement> fragments)
        {
            var graphQlFragmentStatements = fragments as GraphQLFragmentStatement[] ?? fragments.ToArray();
            return CollectionSelectionNode.HasServicesAtOrBelow(graphQlFragmentStatements)
                || ObjectProjectionNode.HasServicesAtOrBelow(graphQlFragmentStatements)
                || ObjectProjectionNode.QueryFields?.Any(f => f.HasServicesAtOrBelow(graphQlFragmentStatements)) == true;
        }

        protected override Expression? GetFieldExpression(
            CompileContext compileContext,
            IServiceProvider? serviceProvider,
            List<GraphQLFragmentStatement> fragments,
            ParameterExpression? docParam,
            object? docVariables,
            ParameterExpression schemaContext,
            bool withoutServiceFields,
            Expression? replacementNextFieldContext,
            List<Type>? possibleNextContextTypes,
            bool contextChanged,
            ParameterReplacer replacer
        )
        {
            Expression? exp;
            // second / last pass
            if (contextChanged || (HasServices && IsRootField))
                exp = ObjectProjectionNode.GetNodeExpression(
                    compileContext,
                    serviceProvider,
                    fragments,
                    docParam,
                    docVariables,
                    schemaContext,
                    withoutServiceFields,
                    replacementNextFieldContext,
                    possibleNextContextTypes,
                    contextChanged,
                    replacer
                );
            else
                exp = GetCollectionToSingleExpression(
                    compileContext,
                    serviceProvider,
                    fragments,
                    withoutServiceFields,
                    replacementNextFieldContext,
                    schemaContext,
                    contextChanged,
                    docParam,
                    docVariables,
                    possibleNextContextTypes,
                    replacer
                );

            return exp;
        }

        private Expression? GetCollectionToSingleExpression(
            CompileContext compileContext,
            IServiceProvider? serviceProvider,
            List<GraphQLFragmentStatement> fragments,
            bool withoutServiceFields,
            Expression? replacementNextFieldContext,
            ParameterExpression schemaContext,
            bool contextChanged,
            ParameterExpression? docParam,
            object? docVariables,
            List<Type>? possibleNextContextTypes,
            ParameterReplacer replacer
        )
        {
            var capMethod = ExpressionUtil.UpdateCollectionNodeFieldExpression(CollectionSelectionNode, CombineExpression);
            var result = CollectionSelectionNode.GetNodeExpression(
                compileContext,
                serviceProvider,
                fragments,
                docParam,
                docVariables,
                schemaContext,
                withoutServiceFields,
                replacementNextFieldContext,
                possibleNextContextTypes,
                contextChanged,
                replacer
            );
            if (result == null)
                return null;

            var genericType = result.Type.GetEnumerableOrArrayType()!;

            // ToList() first to get around this https://github.com/dotnet/efcore/issues/20505
            if (IsRootField)
                result = ExpressionUtil.MakeCallOnEnumerable(nameof(Enumerable.ToList), [genericType], result);

            // rebuild the .First/FirstOrDefault/etc
            Expression exp;
            if (capMethod == null)
                exp = ExpressionUtil.CombineExpressions(result, CombineExpression, replacer);
            else
                exp = ExpressionUtil.MakeCallOnQueryable(capMethod, [genericType], result);
            return exp;
        }
    }
}
