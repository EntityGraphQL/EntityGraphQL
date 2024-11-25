using System.Linq.Expressions;

namespace EntityGraphQL.Tests.Util;

public enum AssertExpressionType
{
    Call,
    Conditional,
    MemberInit,
    Any,
    MemberBinding,
    Constant,
}

public class AssertExpression(AssertExpressionType type, params object?[] arguments)
{
    public AssertExpressionType Type { get; } = type;
    public object?[] Arguments { get; } = arguments;

    public static AssertExpression Any()
    {
        return new AssertExpression(AssertExpressionType.Any);
    }

    public static AssertExpression AnyOfType(Type type)
    {
        return new AssertExpression(AssertExpressionType.Any, type);
    }

    public static AssertExpression Call(AssertExpression? calledOn, string methodName, params AssertExpression[] arguments)
    {
        return new AssertExpression(AssertExpressionType.Call, [calledOn, methodName, arguments]);
    }

    public static AssertExpression Conditional(AssertExpression test, AssertExpression ifTrue, AssertExpression ifFalse)
    {
        return new AssertExpression(AssertExpressionType.Conditional, [test, ifTrue, ifFalse]);
    }

    public static AssertExpression MemberInit(params AssertExpression[] value)
    {
        return new AssertExpression(AssertExpressionType.MemberInit, [.. value]);
    }

    public static AssertExpression MemberBinding(string memberName, AssertExpression assertExpression)
    {
        return new AssertExpression(AssertExpressionType.MemberBinding, [memberName, assertExpression]);
    }

    public static void Matches(AssertExpression expected, Expression e)
    {
        if (expected.Type == AssertExpressionType.Any)
        {
            if (expected.Arguments.Length > 0 && e.Type != (Type)expected.Arguments[0]!)
                throw new Exception($"Expected type {expected.Arguments[0]} found {e.Type}");
            return;
        }

        if (expected.Type == AssertExpressionType.Call)
        {
            if (e.NodeType != ExpressionType.Call)
                throw new Exception($"Expected Call expression found {e.NodeType}");
            var callExp = (MethodCallExpression)e;
            if (callExp.Method.Name != (string)expected.Arguments[1]!)
                throw new Exception($"Method name mismatch expected {expected.Arguments[1]} found {callExp.Method.Name}");
            if (callExp.Arguments.Count != ((AssertExpression[])expected.Arguments[2]!).Length)
                throw new Exception($"Argument count mismatch for call, expected {((AssertExpression[])expected.Arguments[2]!).Length} found {callExp.Arguments.Count}");

            // TODO: callExp.Object check

            for (var i = 0; i < callExp.Arguments.Count; i++)
            {
                Matches(((AssertExpression[])expected.Arguments[2]!)[i], callExp.Arguments[i]);
            }
        }
        else if (expected.Type == AssertExpressionType.Conditional)
        {
            if (e.NodeType != ExpressionType.Conditional)
                throw new Exception($"Expected Conditional expression found {e.NodeType}");
            var condExp = (ConditionalExpression)e;
            if (expected.Arguments.Length != 3)
                throw new Exception($"Argument count mismatch for AssertExpression, expected 3 found {expected.Arguments.Length}");
            if (expected.Arguments[0] is AssertExpression ae)
            {
                Matches(ae, condExp.Test);
            }
            if (expected.Arguments[1] is AssertExpression aeTrue)
            {
                Matches(aeTrue, condExp.IfTrue);
            }
            if (expected.Arguments[2] is AssertExpression aeFalse)
            {
                Matches(aeFalse, condExp.IfFalse);
            }
        }
        else if (expected.Type == AssertExpressionType.MemberInit)
        {
            if (e.NodeType != ExpressionType.MemberInit)
                throw new Exception($"Expected MemberInit expression found {e.NodeType}");
            var memberInitExp = (MemberInitExpression)e;
            if (memberInitExp.Bindings.Count != expected.Arguments.Length)
                throw new Exception($"Binding count mismatch, expected {expected.Arguments.Length} found {memberInitExp.Bindings.Count}");
            for (var i = 0; i < expected.Arguments.Length; i++)
            {
                if (expected.Arguments[i] is AssertExpression ae)
                {
                    if (ae.Type != AssertExpressionType.MemberBinding)
                        throw new Exception($"Expected MemberBinding AssertExpression found {ae.Type}");
                    var binding = memberInitExp.Bindings[i] as MemberAssignment;
                    if (binding!.Member.Name != (string)ae.Arguments[0]!)
                        throw new Exception($"Member name mismatch expected {ae.Arguments[0]} found {binding.Member.Name}");
                    Matches((AssertExpression)ae.Arguments[1]!, binding.Expression);
                }
                else
                {
                    throw new Exception($"Expected MemberBinding of type AssertExpression found {expected.Arguments[i]!.GetType()}");
                }
            }
        }
        else if (expected.Type == AssertExpressionType.Constant)
        {
            if (e.NodeType != ExpressionType.Constant)
                throw new Exception($"Expected Constant expression found {e.NodeType}");
            var constantExp = (ConstantExpression)e;
            if ((constantExp.Value == null && expected.Arguments[0] == null) || constantExp.Value?.Equals(expected.Arguments[0]) == false)
                throw new Exception($"Constant value mismatch expected {expected.Arguments[0]} found {constantExp.Value}");
        }
        return;
    }

    public static AssertExpression Constant(bool value)
    {
        return new AssertExpression(AssertExpressionType.Constant, [value]);
    }
}
