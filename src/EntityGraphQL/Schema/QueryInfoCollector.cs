using System.Collections.Generic;
using System.Linq;
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
    public static QueryInfo CollectQueryInfo(ExecutableGraphQLStatement operation, List<GraphQLFragmentStatement> fragments)
    {
        var queryInfo = new QueryInfo { OperationType = GetOperationType(operation), OperationName = operation.Name };

        // Collect field information from the operation
        CollectFieldsFromOperation(operation, fragments, queryInfo.TypesQueried);

        // Set totals
        queryInfo.TotalTypesQueried = queryInfo.TypesQueried.Count;
        queryInfo.TotalFieldsQueried = queryInfo.TypesQueried.Values.Sum(fields => fields.Count);

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

    private static void CollectFieldsFromOperation(ExecutableGraphQLStatement operation, List<GraphQLFragmentStatement> fragments, Dictionary<string, List<string>> typesQueried)
    {
        foreach (var field in operation.QueryFields)
        {
            CollectFieldsFromField(field, fragments, typesQueried);
        }
    }

    private static void CollectFieldsFromField(BaseGraphQLField field, List<GraphQLFragmentStatement> fragments, Dictionary<string, List<string>> typesQueried)
    {
        // Handle fragment spreads - don't count the fragment spread itself as a field, just expand its fields
        if (field is GraphQLFragmentSpreadField fragmentSpread)
        {
            var fragment = fragments.FirstOrDefault(f => f.Name == fragmentSpread.Name);
            if (fragment != null)
            {
                foreach (var fragmentField in fragment.QueryFields)
                {
                    CollectFieldsFromField(fragmentField, fragments, typesQueried);
                }
            }
        }
        else
        {
            // Get the type name for this field (only for non-fragment fields)
            var typeName = GetFieldTypeName(field);
            if (typeName != null)
            {
                if (!typesQueried.ContainsKey(typeName))
                    typesQueried[typeName] = new List<string>();

                if (!typesQueried[typeName].Contains(field.Name))
                    typesQueried[typeName].Add(field.Name);
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
                CollectFieldsFromField(subField, fragments, typesQueried);
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
            // For root fields, try to determine the root type based on field type
            // if (field.IsRootField)
            // {
            //     // Check if this is a mutation field
            //     if (field is GraphQLMutationField)
            //     {
            //         return "Mutation";
            //     }

            //     // Check if this is a subscription field
            //     if (field is GraphQLSubscriptionField)
            //     {
            //         return "Subscription";
            //     }

            //     // Default to Query for query fields
            //     return "Query";
            // }
            // else
            {
                // For non-root fields, try to get the GraphQL type name
                var contextType = field.Schema.GetSchemaType(field.Schema.QueryContextType, false, null);
                return contextType?.Name ?? field.Schema.QueryContextType.Name;
            }
        }

        return null;
    }
}
