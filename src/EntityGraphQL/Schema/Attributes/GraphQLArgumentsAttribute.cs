using System;

namespace EntityGraphQL.Schema;
/// <summary>
/// Use on your mutation/subscription/field argument class.
/// Properties on this class will be used as arguments to the method.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Parameter)]
public class GraphQLArgumentsAttribute : Attribute { }
