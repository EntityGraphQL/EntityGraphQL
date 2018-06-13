using System;
using System.Linq.Expressions;

namespace EntityQueryLanguage.Compiler
{
    public class ExpressionUtil
    {
        public static ExpressionResult MakeExpressionCall(Type[] types, string methodName, Type[] genericTypes, params Expression[] parameters)
        {
            foreach (var t in types)
            {
                // Please tell me a better way to do this!
                try
                {
                    //  Console.WriteLine($"Call({t}, {methodName}, {genericTypes}, {parameters.First()})");
                    return (ExpressionResult)Expression.Call(t, methodName, genericTypes, parameters);
                }
                catch (InvalidOperationException)
                {
                    continue; // to next type
                }
            }
            var typesStr = string.Join<Type>(", ", types);
            throw new EqlCompilerException($"Could not find extension method {methodName} on types {typesStr}");
        }

        public static MemberExpression CheckAndGetMemberExpression<TBaseType, TReturn>(Expression<Func<TBaseType, TReturn>> fieldSelection)
        {
            var exp = fieldSelection.Body;
            if (exp.NodeType == ExpressionType.Convert)
                exp = ((UnaryExpression)exp).Operand;

            if (exp.NodeType != ExpressionType.MemberAccess)
                throw new ArgumentException("fieldSelection should be a property or field accessor expression only. E.g (t) => t.MyField", "fieldSelection");
            return (MemberExpression)exp;
        }
    }
}