using System;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Schema.QueryLimits;

/// <summary>
/// Marks a field as rate-limited. Acquisition happens before query execution — no resolver code
/// runs on denial. Multiple extensions on the same field are honored and all must succeed.
/// </summary>
public sealed class FieldRateLimitExtension : BaseFieldExtension
{
    public string PolicyName { get; }
    public bool UserSpecific { get; }

    public FieldRateLimitExtension(string policyName, bool userSpecific)
    {
        if (string.IsNullOrWhiteSpace(policyName))
            throw new ArgumentException("Policy name must not be empty", nameof(policyName));
        PolicyName = policyName;
        UserSpecific = userSpecific;
    }
}

/// <summary>
/// Fluent helpers for tagging fields with a rate-limit policy.
/// </summary>
public static class FieldRateLimitExtensions
{
    /// <summary>
    /// Tag this field with a rate-limit policy. Acquisition happens once per selection per request before
    /// any resolver runs. Repeated calls stack — a field can belong to multiple policies (e.g. a global
    /// bucket and a user-specific bucket) and all must succeed.
    /// </summary>
    /// <param name="field">The field to rate-limit.</param>
    /// <param name="policyName">Name of the policy as registered with your <see cref="IFieldRateLimitService"/>.</param>
    /// <param name="userSpecific">If true, the permit is partitioned by the request's user key.</param>
    public static IField AddRateLimit(this IField field, string policyName, bool userSpecific = false)
    {
        field.AddExtension(new FieldRateLimitExtension(policyName, userSpecific));
        return field;
    }

    /// <summary>
    /// Tag every field on a type with the same rate-limit policy.
    /// </summary>
    public static T AddRateLimit<T>(this T type, string policyName, bool userSpecific = false)
        where T : ISchemaType
    {
        foreach (var field in type.GetFields())
            field.AddRateLimit(policyName, userSpecific);
        return type;
    }
}
