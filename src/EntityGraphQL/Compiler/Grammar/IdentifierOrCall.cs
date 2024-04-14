using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.Grammar;

internal sealed class IdentifierOrCall : IExpression
{
    public IdentifierOrCall(string name, List<IExpression>? arguments = null)
    {
        Name = name;
        Arguments = arguments;
    }
    public Type Type => throw new NotImplementedException();

    public string Name { get; }
    public List<IExpression>? Arguments { get; }
    public bool IsCall => Arguments?.Count > 0;

    public Expression Compile(Expression? context, ISchemaProvider? schema, IMethodProvider methodProvider)
    {
        throw new NotImplementedException();
    }
}
