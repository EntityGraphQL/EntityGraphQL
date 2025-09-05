namespace EntityGraphQL.Compiler.Util;

/// <summary>
/// Helper used only inside expression trees. We wrap service-backed field expressions
/// with a call to this method so later visitors can reliably detect service usage.
/// The method is a no-op at runtime and just returns the value.
/// </summary>
public static class ServiceExpressionMarker
{
    public static T MarkService<T>(T value) => value;
}
