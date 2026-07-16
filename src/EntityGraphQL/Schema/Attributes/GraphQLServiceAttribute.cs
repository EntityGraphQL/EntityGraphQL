using System;

namespace EntityGraphQL.Schema;

/// <summary>
/// Marks a [GraphQLField] method parameter as a DI service when its type would otherwise be rejected.
/// A parameter whose type is a schema entity type (and not the field's context type) fails schema build -
/// it is almost always a mistyped context parameter that would silently resolve from the service provider
/// at query time. Apply this attribute for the rare case where a schema entity type really is registered
/// in DI and intended as a service.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class GraphQLServiceAttribute : Attribute { }
