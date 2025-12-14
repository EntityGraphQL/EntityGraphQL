using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;

namespace EntityGraphQL.AspNet;

/// <summary>
/// Extension methods to add ASP.NET policy-based authorization to GraphQL fields and types
/// </summary>
public static class PolicyAuthorizationExtensions
{
    /// <summary>
    /// To access this field all policies listed here are required
    /// </summary>
    /// <param name="field">The field to add policy requirements to</param>
    /// <param name="policies">The policies required</param>
    /// <returns>The field for method chaining</returns>
    public static IField RequiresAllPolicies(this IField field, params string[] policies)
    {
        field.RequiredAuthorization ??= new RequiredAuthorization();
        AddAllPolicies(field.RequiredAuthorization, policies);
        return field;
    }

    /// <summary>
    /// To access this field any policy listed is required
    /// </summary>
    /// <param name="field">The field to add policy requirements to</param>
    /// <param name="policies">The policies required (any one of them)</param>
    /// <returns>The field for method chaining</returns>
    public static IField RequiresAnyPolicy(this IField field, params string[] policies)
    {
        field.RequiredAuthorization ??= new RequiredAuthorization();
        AddAnyPolicy(field.RequiredAuthorization, policies);
        return field;
    }

    /// <summary>
    /// To access this type all policies listed here are required
    /// </summary>
    /// <param name="schemaType">The type to add policy requirements to</param>
    /// <param name="policies">The policies required</param>
    /// <returns>The type for method chaining</returns>
    public static SchemaType<TBaseType> RequiresAllPolicies<TBaseType>(this SchemaType<TBaseType> schemaType, params string[] policies)
    {
        schemaType.RequiredAuthorization ??= new RequiredAuthorization();
        AddAllPolicies(schemaType.RequiredAuthorization, policies);
        return schemaType;
    }

    /// <summary>
    /// To access this type any of the policies listed is required
    /// </summary>
    /// <param name="schemaType">The type to add policy requirements to</param>
    /// <param name="policies">The policies required (any one of them)</param>
    /// <returns>The type for method chaining</returns>
    public static SchemaType<TBaseType> RequiresAnyPolicy<TBaseType>(this SchemaType<TBaseType> schemaType, params string[] policies)
    {
        schemaType.RequiredAuthorization ??= new RequiredAuthorization();
        AddAnyPolicy(schemaType.RequiredAuthorization, policies);
        return schemaType;
    }

    /// <summary>
    /// Get the policies from a RequiredAuthorization object
    /// </summary>
    public static IEnumerable<IEnumerable<string>>? GetPolicies(this RequiredAuthorization requiredAuthorization)
    {
        if (requiredAuthorization.TryGetData(PolicyOrRoleBasedAuthorization.PoliciesKey, out var policies))
        {
            return policies;
        }
        return null;
    }

    private static void AddAnyPolicy(RequiredAuthorization auth, params string[] policies)
    {
        var policyList = GetOrCreatePolicyList(auth);
        policyList.Add(policies.ToList());
    }

    private static void AddAllPolicies(RequiredAuthorization auth, params string[] policies)
    {
        var policyList = GetOrCreatePolicyList(auth);
        policyList.AddRange(policies.Select(p => new List<string> { p }));
    }

    private static List<List<string>> GetOrCreatePolicyList(RequiredAuthorization auth)
    {
        if (!auth.TryGetData(PolicyOrRoleBasedAuthorization.PoliciesKey, out var policyList) || policyList == null)
        {
            policyList = [];
            auth.SetData(PolicyOrRoleBasedAuthorization.PoliciesKey, policyList);
        }
        return policyList;
    }
}
