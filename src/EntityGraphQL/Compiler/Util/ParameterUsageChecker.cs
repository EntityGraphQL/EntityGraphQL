using System.Linq.Expressions;

namespace EntityGraphQL.Compiler.Util;

/// <summary>Whether an expression references a given parameter.</summary>
internal sealed class ParameterUsageChecker(ParameterExpression parameter) : ExpressionVisitor
{
    private bool found;

    public static bool Uses(Expression expression, ParameterExpression parameter)
    {
        var checker = new ParameterUsageChecker(parameter);
        checker.Visit(expression);
        return checker.found;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (node == parameter)
            found = true;
        return base.VisitParameter(node);
    }
}
