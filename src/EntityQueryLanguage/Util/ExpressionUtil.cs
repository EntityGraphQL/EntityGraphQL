using System;
using System.Linq.Expressions;

namespace EntityQueryLanguage.Util
{
    public class ExpressionUtil
    {
        public static Expression MakeExpressionCall(Type[] types, string methodName, Type[] genericTypes, params Expression[] parameters)
        {
            foreach (var t in types)
            {
                // Please tell me a better way to do this!
                try
                {
                    //  Console.WriteLine($"Call({t}, {methodName}, {genericTypes}, {parameters.First()})");
                    return Expression.Call(t, methodName, genericTypes, parameters);
                }
                catch (InvalidOperationException)
                {
                    continue; // to next type
                }
            }
            var typesStr = string.Join<Type>(", ", types);
            throw new EqlCompilerException($"Could not find extension method {methodName} on types {typesStr}");
        }
    }
}