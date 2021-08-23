using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Compiler.EntityQuery
{
    public interface IMethodProvider
    {
        bool EntityTypeHasMethod(Type context, string methodName);
        ExpressionResult GetMethodContext(ExpressionResult context, string methodName);
        ExpressionResult MakeCall(Expression context, Expression argContext, string methodName, IEnumerable<ExpressionResult> args);
    }
}
