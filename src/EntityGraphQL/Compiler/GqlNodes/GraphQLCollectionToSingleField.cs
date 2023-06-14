using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Directives;
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
    internal class GraphQLCollectionToSingleField : BaseGraphQLQueryField
    {
        private readonly GraphQLListSelectionField collectionSelectionNode;
        private readonly GraphQLObjectProjectionField objectProjectionNode;
        private readonly Expression combineExpression;

        public GraphQLCollectionToSingleField(ISchemaProvider schema, GraphQLListSelectionField collectionNode, GraphQLObjectProjectionField objectProjectionNode, Expression combineExpression)
            : base(schema, null, objectProjectionNode.Name, objectProjectionNode.NextFieldContext, objectProjectionNode.RootParameter, objectProjectionNode.ParentNode, null)
        {
            collectionSelectionNode = collectionNode;
            // do not call tolist as we end up calling First()/etc
            collectionSelectionNode.AllowToList = false;
            this.objectProjectionNode = objectProjectionNode;
            this.combineExpression = combineExpression;
        }

        public override bool HasAnyServices(IEnumerable<GraphQLFragmentStatement> fragments)
        {
            var graphQlFragmentStatements = fragments as GraphQLFragmentStatement[] ?? fragments.ToArray();
            return collectionSelectionNode.HasAnyServices(graphQlFragmentStatements) || objectProjectionNode.HasAnyServices(graphQlFragmentStatements) || objectProjectionNode.QueryFields?.Any(f => f.HasAnyServices(graphQlFragmentStatements)) == true;
        }

        protected override Expression? GetFieldExpression(CompileContext compileContext, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext, bool isRoot, bool contextChanged, ParameterReplacer replacer)
        {
            Expression? exp;
            // this is a first pass || just a single pass
            if (withoutServiceFields || !HasAnyServices(fragments) && isRoot)
            {
                exp = GetCollectionToSingleExpression(compileContext, serviceProvider, fragments, withoutServiceFields, replacementNextFieldContext, isRoot, schemaContext, contextChanged, docParam, docVariables, replacer);
            }
            else
            {
                // second / last pass
                exp = objectProjectionNode.GetNodeExpression(compileContext, serviceProvider, fragments, docParam, docVariables, schemaContext, withoutServiceFields, replacementNextFieldContext, isRoot, contextChanged, replacer);
            }

            if (exp == null)
                return null;

            return exp;
        }

        private Expression? GetCollectionToSingleExpression(CompileContext compileContext, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, Expression? replacementNextFieldContext, bool isRoot, ParameterExpression schemaContext, bool contextChanged, ParameterExpression? docParam, object? docVariables, ParameterReplacer replacer)
        {
            var capMethod = ExpressionUtil.UpdateCollectionNodeFieldExpression(collectionSelectionNode, combineExpression);
            var result = collectionSelectionNode.GetNodeExpression(compileContext, serviceProvider, fragments, docParam, docVariables, schemaContext, withoutServiceFields, replacementNextFieldContext, isRoot, contextChanged, replacer);
            if (result == null)
                return null;

            var genericType = result.Type.GetEnumerableOrArrayType()!;

            // ToList() first to get around this https://github.com/dotnet/efcore/issues/20505
            if (isRoot)
                result = ExpressionUtil.MakeCallOnEnumerable(nameof(Enumerable.ToList), new[] { genericType }, result);

            // rebuild the .First/FirstOrDefault/etc
            Expression exp;
            if (capMethod == null)
                exp = ExpressionUtil.CombineExpressions(result, combineExpression, replacer);
            else
                exp = ExpressionUtil.MakeCallOnQueryable(capMethod, new[] { genericType }, result);
            return exp;
        }
    }
}