using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Directives
{
    public interface IDirectiveProcessor
    {
        string Name { get; }
        string Description { get; }
        /// <summary>
        /// Return the Type used for the directive arguments
        /// </summary>
        /// <returns></returns>
        Type GetArgumentsType();
        Expression? ProcessExpression(Expression expression, object arguments);
        IEnumerable<ArgType> GetArguments(ISchemaProvider schema, Func<string, string> fieldNamer);
    }
}