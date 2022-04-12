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
            : base(objectProjectionNode.Name, objectProjectionNode.NextFieldContext, objectProjectionNode.RootParameter, objectProjectionNode.ParentNode, null)
        {
            this.collectionSelectionNode = collectionNode;
            this.objectProjectionNode = objectProjectionNode;
            this.combineExpression = combineExpression;

            AddServices(objectProjectionNode.Services);
            AddServices(collectionSelectionNode.Services);
        }

        public override bool HasAnyServices(IEnumerable<GraphQLFragmentStatement> fragments)
        {
            return Services?.Any() == true || objectProjectionNode.QueryFields?.Any(f => f.HasAnyServices(fragments)) == true;
        }

        public override Expression? GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, Dictionary<string, object> parentArguments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext = null, bool isRoot = false, bool contextChanged = false)
        {
            Expression? exp;
            // this is a first pass || just a single pass
            if (withoutServiceFields || !HasAnyServices(fragments) && isRoot)
            {
                exp = GetCollectionToSingleExpression(serviceProvider, fragments, withoutServiceFields, replacementNextFieldContext, isRoot, schemaContext, contextChanged, parentArguments, docParam, docVariables);
            }
            else
            {
                // second / last pass
                exp = objectProjectionNode.GetNodeExpression(serviceProvider, fragments, parentArguments.MergeNew(arguments), docParam, docVariables, schemaContext, withoutServiceFields, replacementNextFieldContext, isRoot, contextChanged);
            }
            if (exp == null)
                return null;

            Services.AddRange(objectProjectionNode.Services);
            Services.AddRange(collectionSelectionNode.Services);
            AddConstantParameters(objectProjectionNode.ConstantParameters);

            return exp;
        }

        private Expression? GetCollectionToSingleExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, Expression? replacementNextFieldContext, bool isRoot, ParameterExpression schemaContext, bool contextChanged, Dictionary<string, object> parentArguments, ParameterExpression? docParam, object? docVariables)
        {
            var capMethod = ExpressionUtil.UpdateCollectionNodeFieldExpression(collectionSelectionNode, combineExpression);
            var result = collectionSelectionNode.GetNodeExpression(serviceProvider, fragments, parentArguments.MergeNew(arguments), docParam, docVariables, schemaContext, withoutServiceFields, replacementNextFieldContext, isRoot, contextChanged);
            if (result == null)
                return null;

            foreach (var item in collectionSelectionNode.ConstantParameters)
            {
                if (!constantParameters.ContainsKey(item.Key))
                    constantParameters.Add(item.Key, item.Value);
            }

            var genericType = result.Type.GetEnumerableOrArrayType()!;

            // ToList() first to get around this https://github.com/dotnet/efcore/issues/20505
            if (isRoot)
                result = ExpressionUtil.MakeCallOnEnumerable("ToList", new Type[] { genericType }, result);

            // rebuild the .First/FirstOrDefault/etc
            Expression exp;
            if (capMethod == null)
                exp = ExpressionUtil.CombineExpressions(result, combineExpression);
            else
                exp = ExpressionUtil.MakeCallOnQueryable(capMethod, new Type[] { genericType }, result);
            return exp;
        }
    }
}