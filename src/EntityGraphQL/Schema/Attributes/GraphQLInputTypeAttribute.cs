using System;

namespace EntityGraphQL.Schema;

/// <summary>
/// Tells EntityGraphQL that this class or parameter type is an InputType in the schema.
/// Use on your mutation/subscription/field argument class.
/// Method parameter will be added as a single argument with an Input Type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Parameter)]
public class GraphQLInputTypeAttribute : Attribute { }