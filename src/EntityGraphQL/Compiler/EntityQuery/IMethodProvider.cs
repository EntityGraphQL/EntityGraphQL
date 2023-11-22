using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler.EntityQuery
{
    public interface IMethodProvider
    {
        bool EntityTypeHasMethod(Type context, string methodName);
        Expression GetMethodContext(Expression context, string methodName);
        Expression MakeCall(Expression context, Expression argContext, string methodName, IEnumerable<Expression>? args, Type? type = null);
    }
}
