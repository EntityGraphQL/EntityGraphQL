using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

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
    public GraphQLListSelectionField CollectionSelectionNode { get; set; }
    public GraphQLObjectProjectionField ObjectProjectionNode { get; set; }
    public Expression CombineExpression { get; set; }
    public override bool IsRootField
    {
        set
        {
            base.IsRootField = value;
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

    public override bool HasServicesAtOrBelow(IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments)
    {
        return CollectionSelectionNode.HasServicesAtOrBelow(fragments) || ObjectProjectionNode.HasServicesAtOrBelow(fragments);
    }

    protected override Expression? GetFieldExpression(
        CompileContext compileContext,
        IServiceProvider? serviceProvider,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        ParameterExpression? docParam,
        IArgumentsTracker? docVariables,
        ParameterExpression schemaContext,
        bool withoutServiceFields,
        Expression? replacementNextFieldContext,
        List<Type>? possibleNextContextTypes,
        bool contextChanged,
        ParameterReplacer replacer
    )
    {
        Expression? exp;
        // A field whose only service is the query context - e.g. .Resolve<MyDbContext>((c, db) => db.Things
        // .FirstOrDefault(t => t.Id == c.ThingId)) - is a database query, so keep the
        // Where().Select().FirstOrDefault() shape below even on the services pass. The object projection
        // builds the selection off the field's expression instead, which repeats the sub-select per selected
        // field and, when this field sits below another context-resolved one, uses ProjectWithNullCheck inside
        // a query the provider has to translate - which it cannot.
        var servicesAreQueryContextOnly = HasServices && Field!.Services.All(s => s.Type == Field.Schema.QueryContextType);
        // second / last pass
        if ((contextChanged || (HasServices && IsRootField)) && !servicesAreQueryContextOnly)
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
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        bool withoutServiceFields,
        Expression? replacementNextFieldContext,
        ParameterExpression schemaContext,
        bool contextChanged,
        ParameterExpression? docParam,
        IArgumentsTracker? docVariables,
        List<Type>? possibleNextContextTypes,
        ParameterReplacer replacer
    )
    {
        var (capMethod, listSelection) = ExpressionUtil.UpdateCollectionNodeFieldExpression(CollectionSelectionNode, CombineExpression);
        var result = listSelection.GetNodeExpression(
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

    public override List<BaseGraphQLField> QueryFields => CollectionSelectionNode.QueryFields;

    public override void AddField(BaseGraphQLField field)
    {
        // both need the fields so we can build the right expression
        // Update the parent node to be the collection node
        // This ensures child fields use the correct parameter context
        field.ParentNode = CollectionSelectionNode;
        CollectionSelectionNode.QueryFields.Add(field);
        ObjectProjectionNode.QueryFields.Add(field);
    }
}
