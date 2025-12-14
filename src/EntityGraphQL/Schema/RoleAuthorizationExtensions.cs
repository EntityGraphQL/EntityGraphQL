using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL.Schema;

/// <summary>
/// Extension methods to add role-based authorization to GraphQL fields and types
/// </summary>
public static class RoleAuthorizationExtensions
{
    internal const string RolesKey = "egql:core:roles";

    /// <summary>
    /// Get the roles from a RequiredAuthorization object
    /// </summary>
    public static IEnumerable<IEnumerable<string>>? GetRoles(this RequiredAuthorization requiredAuthorization)
    {
        if (requiredAuthorization.TryGetData(RolesKey, out var roles))
        {
            return roles;
        }
        return null;
    }

    /// <summary>
    /// To access this field all roles listed here are required
    /// </summary>
    public static IField RequiresAllRoles(this IField field, params string[] roles)
    {
        field.RequiredAuthorization ??= new RequiredAuthorization();
        AddAllRoles(field.RequiredAuthorization, roles);
        return field;
    }

    /// <summary>
    /// To access this field any role listed is required
    /// </summary>
    public static IField RequiresAnyRole(this IField field, params string[] roles)
    {
        field.RequiredAuthorization ??= new RequiredAuthorization();
        AddAnyRole(field.RequiredAuthorization, roles);
        return field;
    }

    /// <summary>
    /// To access this type all roles listed here are required
    /// </summary>
    public static SchemaType<TBaseType> RequiresAllRoles<TBaseType>(this SchemaType<TBaseType> schemaType, params string[] roles)
    {
        schemaType.RequiredAuthorization ??= new RequiredAuthorization();
        AddAllRoles(schemaType.RequiredAuthorization, roles);
        return schemaType;
    }

    /// <summary>
    /// To access this type any of the roles listed is required
    /// </summary>
    public static SchemaType<TBaseType> RequiresAnyRole<TBaseType>(this SchemaType<TBaseType> schemaType, params string[] roles)
    {
        schemaType.RequiredAuthorization ??= new RequiredAuthorization();
        AddAnyRole(schemaType.RequiredAuthorization, roles);
        return schemaType;
    }

    /// <summary>
    /// Clear role requirements
    /// </summary>
    public static void ClearRoles(this RequiredAuthorization requiredAuthorization)
    {
        requiredAuthorization.RemoveData(RolesKey);
    }

    /// <summary>
    /// Add roles to a RequiredAuthorization object where any role in the list satisfies (OR)
    /// </summary>
    public static void RequiresAnyRole(this RequiredAuthorization auth, params string[] roles)
    {
        AddAnyRole(auth, roles);
    }

    /// <summary>
    /// Add roles to a RequiredAuthorization object where all roles are required (AND)
    /// </summary>
    public static void RequiresAllRoles(this RequiredAuthorization auth, params string[] roles)
    {
        AddAllRoles(auth, roles);
    }

    private static void AddAnyRole(RequiredAuthorization auth, params string[] roles)
    {
        var roleList = GetOrCreateRoleList(auth);
        roleList.Add(roles.ToList());
    }

    private static void AddAllRoles(RequiredAuthorization auth, params string[] roles)
    {
        var roleList = GetOrCreateRoleList(auth);
        roleList.AddRange(roles.Select(r => new List<string> { r }));
    }

    private static List<List<string>> GetOrCreateRoleList(RequiredAuthorization auth)
    {
        if (!auth.TryGetData(RolesKey, out var roleList) || roleList == null)
        {
            roleList = [];
            auth.SetData(RolesKey, roleList);
        }
        return roleList;
    }
}
