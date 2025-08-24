using System;
using System.Linq.Expressions;
using System.Reflection;

namespace EntityGraphQL.Compiler.EntityQuery;

/// <summary>
/// Contains information about a registered method for use in filter expressions.
/// This unified class handles both extension methods and direct methods.
/// </summary>
public class RegisteredMethodInfo
{
    /// <summary>
    /// The MethodInfo of the method.
    /// </summary>
    public MethodInfo Method { get; set; } = null!;

    /// <summary>
    /// The name to use for this method in filter expressions.
    /// </summary>
    public string MethodName { get; set; } = null!;

    /// <summary>
    /// The type that this method can be called on.
    /// </summary>
    public Type MethodContextType { get; set; } = null!;

    /// <summary>
    /// Dynamic type predicate function for efficient type checking.
    /// When provided, this takes precedence over MethodContextType for type compatibility checks.
    /// </summary>
    public Func<Type, bool>? TypePredicate { get; set; }

    /// <summary>
    /// Custom function to make the expression call for delegate methods
    /// </summary>
    public Func<Expression, Expression, string, Expression[], Expression>? MakeCallFunc { get; set; }

    /// <summary>
    /// Indicates whether this is a default method or a custom method
    /// </summary>
    public MethodOrigin Origin { get; set; }
}

/// <summary>
/// Indicates whether the method is a default method or a custom method.
/// </summary>
public enum MethodOrigin
{
    /// <summary>
    /// A default method that comes pre-registered (like contains, startsWith, etc.)
    /// </summary>
    Default,

    /// <summary>
    /// A custom method registered by the user
    /// </summary>
    Custom,
}
