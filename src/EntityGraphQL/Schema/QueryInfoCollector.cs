using System.Collections.Generic;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Schema;

/// <summary>
/// Collects information about a GraphQL query execution
/// </summary>
internal static class QueryInfoCollector
{
    /// <summary>
    /// Collect query information from an executed operation
    /// </summary>
    public static QueryInfo CollectQueryInfo(ExecutableGraphQLStatement operation, IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments)
    {
        var queryInfo = new QueryInfo { OperationType = GetOperationType(operation), OperationName = operation.Name };
        var totalFieldCount = 0;

        // Collect field information from the operation
        CollectFieldsFromOperation(operation, fragments, queryInfo.TypesQueried, ref totalFieldCount);

        queryInfo.TotalTypesQueried = queryInfo.TypesQueried.Count;
        queryInfo.TotalFieldsQueried = totalFieldCount;

        return queryInfo;
    }

    private static GraphQLOperationType GetOperationType(ExecutableGraphQLStatement operation)
    {
        return operation.GetType().Name switch
        {
            nameof(GraphQLQueryStatement) => GraphQLOperationType.Query,
            nameof(GraphQLMutationStatement) => GraphQLOperationType.Mutation,
            nameof(GraphQLSubscriptionStatement) => GraphQLOperationType.Subscription,
            _ => GraphQLOperationType.Query,
        };
    }

    private static void CollectFieldsFromOperation(
        ExecutableGraphQLStatement operation,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragmentLookup,
        Dictionary<string, HashSet<string>> typesQueried,
        ref int totalFieldCount
    )
    {
        foreach (var field in operation.QueryFields)
        {
            CollectFieldsFromField(field, fragmentLookup, typesQueried, ref totalFieldCount);
        }
    }

    private static void CollectFieldsFromField(
        BaseGraphQLField field,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragmentLookup,
        Dictionary<string, HashSet<string>> typesQueried,
        ref int totalFieldCount
    )
    {
        // Handle fragment spreads - don't count the fragment spread itself as a field, just expand its fields
        if (field is GraphQLFragmentSpreadField fragmentSpread)
        {
            if (fragmentLookup.TryGetValue(fragmentSpread.Name, out var fragment))
            {
                foreach (var fragmentField in fragment.QueryFields)
                {
                    CollectFieldsFromField(fragmentField, fragmentLookup, typesQueried, ref totalFieldCount);
                }
            }
        }
        else
        {
            // Get the type name for this field (only for non-fragment fields)
            var typeName = GetFieldTypeName(field);
            if (typeName != null)
            {
                // Use HashSet for O(1) duplicate checking instead of O(n) Contains on List
                if (!typesQueried.TryGetValue(typeName, out var fieldSet))
                {
                    fieldSet = new HashSet<string>();
                    typesQueried[typeName] = fieldSet;
                }

                // Only increment count if it's a new field
                if (fieldSet.Add(field.Name))
                {
                    totalFieldCount++;
                }
            }

            // Recursively collect fields from nested selections
            var queryFields = field.QueryFields ?? [];
            if (field is GraphQLMutationField mutationField)
            {
                // For mutation fields, we can also collect fields from the result selection
                if (mutationField.ResultSelection != null)
                {
                    queryFields = mutationField.ResultSelection.QueryFields;
                }
            }
            if (field is GraphQLSubscriptionField subscriptionField)
            {
                // For subscription fields, we can also collect fields from the result selection
                if (subscriptionField.ResultSelection != null)
                {
                    queryFields = subscriptionField.ResultSelection.QueryFields;
                }
            }
            foreach (var subField in queryFields)
            {
                CollectFieldsFromField(subField, fragmentLookup, typesQueried, ref totalFieldCount);
            }
        }
    }

    private static string? GetFieldTypeName(BaseGraphQLField field)
    {
        // Try to get the GraphQL type name from the field's type
        if (field.Field?.FromType != null)
        {
            // Use the GraphQL type name instead of .NET type name
            return field.Field.FromType.Name;
        }

        // Fallback: try to get from schema context types
        if (field.Schema != null)
        {
            // For non-root fields, try to get the GraphQL type name
            var contextType = field.Schema.GetSchemaType(field.Schema.QueryContextType, false, null);
            return contextType?.Name ?? field.Schema.QueryContextType.Name;
        }

        return null;
    }
}
