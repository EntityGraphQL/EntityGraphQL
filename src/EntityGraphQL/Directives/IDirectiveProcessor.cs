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
#pragma warning disable CA1716
        List<ExecutableDirectiveLocation> On { get; }
#pragma warning restore CA1716
        /// <summary>
        /// Return the Type used for the directive arguments
        /// </summary>
        /// <returns></returns>
        Type GetArgumentsType();
        Expression? ProcessExpression(Expression expression, object? arguments);
        IDictionary<string, ArgType> GetArguments(ISchemaProvider schema);
        BaseGraphQLField? ProcessField(BaseGraphQLField field, object? arguments);
    }
}