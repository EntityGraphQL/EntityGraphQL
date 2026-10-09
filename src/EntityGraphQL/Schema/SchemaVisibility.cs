using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EntityGraphQL.Schema;

/// <summary>
/// What a user may see of a schema - shared by introspection and <see cref="SchemaGenerator"/> so both describe the same,
/// self-consistent schema.
///
/// Checking each type and field on its own is not enough: hiding one thing can leave another referring to it. A type whose
/// fields are all protected is empty (not valid GraphQL) so it is hidden, which hides the fields returning it, which can
/// empty another type, and so on. The same goes for a field whose argument is a hidden enum or input type, a hidden
/// interface in an implements list, and a union whose members are all hidden. So the hidden set is computed up front and
/// repeated until nothing changes.
///
/// No request context means no filtering, exactly as before.
/// </summary>
internal sealed class SchemaVisibility
{
    // One per request context - introspection resolves __Type.fields per type, so this is asked for many times per request
    private static readonly ConditionalWeakTable<QueryRequestContext, SchemaVisibility> cache = new();

    private static readonly SchemaVisibility everything = new(null, null);

    private readonly ISchemaProvider? schema;
    private readonly QueryRequestContext? requestContext;

    /// <summary>Names of types hidden from the user. Types not in the schema at all (unknown to it) are never hidden here.</summary>
    private readonly HashSet<string> hiddenTypes = new();

    private SchemaVisibility(ISchemaProvider? schema, QueryRequestContext? requestContext)
    {
        this.schema = schema;
        this.requestContext = requestContext;
        if (schema != null && requestContext != null)
            ComputeHiddenTypes(schema);
    }

    public static SchemaVisibility For(ISchemaProvider schema, QueryRequestContext? requestContext)
    {
        if (requestContext == null)
            return everything;

        if (cache.TryGetValue(requestContext, out var existing) && ReferenceEquals(existing.schema, schema))
            return existing;

        var visibility = new SchemaVisibility(schema, requestContext);
        cache.AddOrUpdate(requestContext, visibility);
        return visibility;
    }

    public bool IsFiltering => requestContext != null;

    public bool IsTypeVisible(ISchemaType schemaType) => !hiddenTypes.Contains(schemaType.Name);

    /// <summary>A field is visible when it is authorized, its type is visible and every argument's type is visible.</summary>
    public bool IsFieldVisible(IField field)
    {
        if (requestContext == null)
            return true;

        if (!IsAuthorized(field.RequiredAuthorization) || hiddenTypes.Contains(field.ReturnType.SchemaType.Name))
            return false;

        return field.ArgumentsAreInternal || field.Arguments.Values.All(arg => !hiddenTypes.Contains(arg.Type.SchemaType.Name));
    }

    private bool IsAuthorized(RequiredAuthorization? requiredAuthorization) => requestContext == null || requestContext.AuthorizationService.IsAuthorized(requestContext.User, requiredAuthorization);

    private static bool IsInternal(IField field) => field.Name.StartsWith("__", StringComparison.InvariantCulture);

    private void ComputeHiddenTypes(ISchemaProvider schema)
    {
        var rootQueryName = schema.QueryContextName;
        var allTypes = schema.GetNonContextTypes().Append(schema.GetSchemaType(rootQueryName, null)).GroupBy(t => t.Name).Select(g => g.First()).ToList();

        foreach (var type in allTypes)
        {
            // the query root always exists - an empty one is still a query root
            if (type.Name != rootQueryName && !IsAuthorized(type.RequiredAuthorization))
                hiddenTypes.Add(type.Name);
        }

        bool changed;
        do
        {
            changed = false;
            foreach (var type in allTypes)
            {
                if (hiddenTypes.Contains(type.Name) || type.IsScalar || type.IsEnum || type.Name == rootQueryName)
                    continue;
                // the mutation/subscription roots are left out when they have no visible fields, nothing references them
                if (type.Name == schema.Mutation().SchemaType.Name || type.Name == schema.Subscription().SchemaType.Name)
                    continue;

                // only types the filtering emptied - a type that never had fields or members (an interface turned union,
                // a class used only as a base type) is output exactly as it is without filtering
                bool emptied;
                if (type.GqlType == GqlTypes.Union)
                {
                    emptied = type.PossibleTypesReadOnly.Count > 0 && type.PossibleTypesReadOnly.All(possible => hiddenTypes.Contains(possible.Name));
                }
                else
                {
                    var fields = type.GetFields().Where(field => !IsInternal(field)).ToList();
                    emptied = fields.Count > 0 && !fields.Any(IsFieldVisible);
                }

                if (emptied)
                {
                    hiddenTypes.Add(type.Name);
                    changed = true;
                }
            }
        } while (changed);
    }
}
