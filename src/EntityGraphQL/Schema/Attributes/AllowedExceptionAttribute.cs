using System;

namespace EntityGraphQL.Schema;

/// <summary>
/// Exceptions markwed with this attribute will be allowed to have their details included in the response results.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class AllowedExceptionAttribute : Attribute
{
    public AllowedExceptionAttribute()
    {
    }
}