using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL.Schema;

/// <summary>
/// Details on the authorization required by a field or type.
/// Uses a keyed data structure to allow different authorization implementations to store their requirements.
///
/// Data is keyed by your authorization implementation and the value is a list of list of strings to allow for AND/OR combinations
/// For example:
/// To require any of roles A or B and also any of roles C or D you would have:
/// [ [ "A", "B" ], [ "C", "D" ] ]
///
/// Core EntityGraphQL provides a role-based authorization implementation via extension methods
/// </summary>
public class RequiredAuthorization
{
    /// <summary>
    /// Keyed authorization data that can be used by authorization implementations
    /// </summary>
    private readonly Dictionary<string, List<List<string>>> authData = [];
    public IReadOnlyDictionary<string, List<List<string>>> AuthData => authData;

    public bool Any() => authData.Count > 0;

    /// <summary>
    /// Set keyed authorization data
    /// </summary>
    public void SetData(string key, List<List<string>> value)
    {
        authData[key] = value;
    }

    /// <summary>
    /// Get keyed authorization data
    /// </summary>
    public bool TryGetData(string key, out List<List<string>>? value)
    {
        if (authData.TryGetValue(key, out var obj))
        {
            value = obj;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Remove keyed authorization data
    /// </summary>
    public bool RemoveData(string key)
    {
        return authData.Remove(key);
    }

    /// <summary>
    /// Clear all authorization data
    /// </summary>
    public void Clear()
    {
        authData.Clear();
    }

    public RequiredAuthorization Concat(RequiredAuthorization requiredAuthorization)
    {
        var newRequiredAuthorization = new RequiredAuthorization();

        // Merge keyed data - need to handle list merging specially
        foreach (var kvp in authData)
        {
            newRequiredAuthorization.authData[kvp.Key] = kvp.Value.Select(group => group.ToList()).ToList();
        }

        foreach (var kvp in requiredAuthorization.authData)
        {
            if (newRequiredAuthorization.authData.TryGetValue(kvp.Key, out var existing))
            {
                existing.AddRange(kvp.Value.Select(group => group.ToList()));
            }
            else
            {
                newRequiredAuthorization.authData[kvp.Key] = kvp.Value.Select(group => group.ToList()).ToList();
            }
        }

        return newRequiredAuthorization;
    }
}
