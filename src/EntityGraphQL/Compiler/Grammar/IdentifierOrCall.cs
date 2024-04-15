using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.Grammar;

internal sealed class IdentifierOrCall(string name, List<IExpression>? arguments = null) : IExpression
{
    public Type Type => throw new NotImplementedException();

    public string Name { get; } = name;
    public List<IExpression>? Arguments { get; } = arguments;
    public bool IsCall => Arguments != null;

    public Expression Compile(Expression? context, ISchemaProvider? schema, IMethodProvider methodProvider)
    {
        throw new NotImplementedException();
    }
}
