using System;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.EntityQuery;

namespace EntityGraphQL.Schema.FieldExtensions;

public static class UseFilterExtension
{
    /// <summary>
    /// Update a collection field to implement a filter argument that takes an expression string (e.g. "property1 >= 5")
    /// Only call on a field that returns an IEnumerable
    /// </summary>
    /// <param name="field"></param>
    /// <returns></returns>
    public static IField UseFilter(this IField field)
    {
        field.AddExtension(new FilterExpressionExtension());
        return field;
    }

    /// <summary>
    /// Registers a parser for binary comparisons in filter expressions that converts a string operand to TTarget at compile time.
    /// Applied when one side is string and the other is TTarget or Nullable&lt;TTarget&gt;.
    /// This is a global registration that applies to all filter expressions.
    /// </summary>
    /// <typeparam name="TTarget">Target type to parse string literals into.</typeparam>
    /// <param name="makeParseExpression">Factory that turns a string Expression into a TTarget Expression.</param>
    public static void RegisterLiteralParser<TTarget>(Func<Expression, Expression> makeParseExpression)
    {
        EntityQueryCompiler.RegisterLiteralParser<TTarget>(makeParseExpression);
    }
}

public class UseFilterAttribute : ExtensionAttribute
{
    public override void ApplyExtension(IField field)
    {
        field.UseFilter();
    }
}
