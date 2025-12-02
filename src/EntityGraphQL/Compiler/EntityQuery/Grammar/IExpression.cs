using System;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.EntityQuery.Grammar;

internal interface IExpression
{
    Type Type { get; }
    Expression Compile(Expression? context, EntityQueryParser parser, ISchemaProvider? schema, QueryRequestContext requestContext, IMethodProvider methodProvider);
}

internal sealed class EqlExpression(Expression value) : IExpression
{
    private readonly Expression value = value;

    public Type Type => value.Type;

    public Expression Compile(Expression? context, EntityQueryParser parser, ISchemaProvider? schema, QueryRequestContext requestContext, IMethodProvider methodProvider)
    {
        return value;
    }
}
