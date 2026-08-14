using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

/// <summary>
/// Builds what a resolver is told about the objects it returns: the members the engine will read off them.
/// See <see cref="IFieldSelection"/> for the rules this implements and why they are not simply "the fields the
/// caller asked for".
/// </summary>
internal static class FieldSelectionBuilder
{
    public static FieldSelection Build(
        BaseGraphQLField field,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        ParameterExpression? docParam,
        IArgumentsTracker? docVariables
    )
    {
        // Note the bulk key is not in here: it is read off the parent entity to build the id list, and the
        // loader keys its own dictionary, so nothing is read off the returned object for it.
        var selection = new FieldSelection();
        Collect(field.QueryFields, fragments, selection, docParam, docVariables);
        return selection;
    }

    private static void Collect(
        IEnumerable<BaseGraphQLField> queryFields,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        FieldSelection into,
        ParameterExpression? docParam,
        IArgumentsTracker? docVariables
    )
    {
        foreach (var child in queryFields)
        {
            // @skip/@include - ask the engine rather than reading the directives here, so this cannot drift
            // from what it actually selects
            if (child.IsExcludedByDirectives(docParam, docVariables))
                continue;

            switch (child)
            {
                // a spread's own QueryFields are empty - the fields live on the fragment statement
                case GraphQLFragmentSpreadField spread:
                {
                    var fragment = fragments.GetValueOrDefault(spread.Name);
                    if (fragment != null)
                        Collect(fragment.QueryFields, fragments, into, docParam, docVariables);
                    continue;
                }
                case GraphQLInlineFragmentField inline:
                    Collect(inline.QueryFields, fragments, into, docParam, docVariables);
                    continue;
            }

            // __typename and friends are computed from the schema, nothing is read for them
            if (child.Field == null || child.Name.StartsWith("__", System.StringComparison.Ordinal))
                continue;

            if (child.HasServices)
            {
                // resolved after us and not from our data - all we can usefully return is what its resolver
                // reads off the object (its bulk key, or the members its expression uses)
                AddServiceFieldDependencies(child, into);
                continue;
            }

            var member = MemberNameOf(child.Field) ?? child.Field.Name;
            into.AddField(member, child.Field);
            if (child.QueryFields.Count > 0)
            {
                // read through it - the engine projects the nested object, so its own members are read too
                var childSelection = new FieldSelection();
                Collect(child.QueryFields, fragments, childSelection, docParam, docVariables);
                foreach (var path in childSelection.Paths)
                    into.AddPath([member, .. path.Split('.')], child.Field);
            }
        }
    }

    private static void AddServiceFieldDependencies(BaseGraphQLField child, FieldSelection into)
    {
        var bulkKey = child.Field?.BulkResolver?.DataSelector;
        if (bulkKey?.Parameters.Count > 0)
            into.AddMembersOf(bulkKey.Body, bulkKey.Parameters[0]);

        var extracted = child.Field?.ExtractedFieldsFromServices;
        if (extracted == null)
            return;
        foreach (var dep in extracted)
        {
            foreach (var exp in dep.FieldExpressions)
            {
                if (child.Field!.FieldParam != null)
                    into.AddMembersOf(exp, child.Field.FieldParam);
            }
        }
    }

    /// <summary>The member a field reads, where its expression is a plain member access on the entity.</summary>
    private static string? MemberNameOf(IField field)
    {
        if (field.ResolveExpression is MemberExpression member && member.Expression == field.FieldParam)
            return member.Member.Name;
        return null;
    }
}
