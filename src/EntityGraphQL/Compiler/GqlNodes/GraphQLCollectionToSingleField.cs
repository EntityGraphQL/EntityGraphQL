using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

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

        public GraphQLCollectionToSingleField(GraphQLListSelectionField collectionNode, GraphQLObjectProjectionField objectProjectionNode, Expression combineExpression)
        {
            this.collectionSelectionNode = collectionNode;
            this.objectProjectionNode = objectProjectionNode;
            this.combineExpression = combineExpression;
            this.Name = objectProjectionNode.Name;
            this.RootFieldParameter = objectProjectionNode.RootFieldParameter;

            constantParameters = new Dictionary<ParameterExpression, object>(objectProjectionNode.ConstantParameters);
            this.AddServices(objectProjectionNode.Services);
            this.AddServices(collectionSelectionNode.Services);
        }

        public override bool HasAnyServices(IEnumerable<GraphQLFragmentStatement> fragments)
        {
            return Services?.Any() == true || objectProjectionNode.QueryFields?.Any(f => f.HasAnyServices(fragments)) == true;
        }

        public override ExpressionResult GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields = false, Expression replaceContextWith = null, bool isRoot = false, bool useReplaceContextDirectly = false)
        {
            ExpressionResult exp;
            // this is a first pass || just a single pass
            if (withoutServiceFields || !HasAnyServices(fragments) && isRoot)
            {
                exp = GetCollectionToSingleExpression(serviceProvider, fragments, withoutServiceFields, replaceContextWith, isRoot, useReplaceContextDirectly);
            }
            else
            {
                exp = objectProjectionNode.GetNodeExpression(serviceProvider, fragments, withoutServiceFields, replaceContextWith, isRoot, useReplaceContextDirectly);
            }
            if (exp == null)
                return null;

            exp.AddConstantParameters(objectProjectionNode.ConstantParameters);
            exp.AddServices(Services);
            AddServices(exp.Services);

            return exp;
        }

        private ExpressionResult GetCollectionToSingleExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, Expression replaceContextWith, bool isRoot, bool useReplaceContextDirectly)
        {
            var capMethod = ExpressionUtil.UpdateCollectionNodeFieldExpression(collectionSelectionNode, combineExpression);
            var result = collectionSelectionNode.GetNodeExpression(serviceProvider, fragments, withoutServiceFields, replaceContextWith, isRoot, useReplaceContextDirectly);
            if (result == null)
                return null;

            var genericType = result.Type.GetEnumerableOrArrayType();

            // ToList() first to get around this https://github.com/dotnet/efcore/issues/20505
            if (isRoot)
                result = ExpressionUtil.MakeCallOnEnumerable("ToList", new Type[] { genericType }, result);

            // rebuild the .First/FirstOrDefault/etc
            ExpressionResult exp;
            if (capMethod == null)
                exp = (ExpressionResult)ExpressionUtil.CombineExpressions(result, combineExpression);
            else
                exp = ExpressionUtil.MakeCallOnQueryable(capMethod, new Type[] { genericType }, result);
            exp.AddConstantParameters(result.ConstantParameters);
            exp.AddServices(result.Services);
            return exp;
        }
    }
}