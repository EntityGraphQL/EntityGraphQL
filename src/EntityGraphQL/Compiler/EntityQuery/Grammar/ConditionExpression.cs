using System;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.EntityQuery.Grammar;

internal sealed class ConditionExpression(IExpression condition, IExpression ifTrue, IExpression ifFalse) : IExpression
{
    private readonly IExpression condition = condition;
    private readonly IExpression ifTrue = ifTrue;
    private readonly IExpression ifFalse = ifFalse;

    public Type Type => ifTrue.Type;

    public Expression Compile(Expression? context, ISchemaProvider? schema, QueryRequestContext requestContext, IMethodProvider methodProvider)
    {
        var trueExp = ifTrue.Compile(context, schema, requestContext, methodProvider);
        var falseExp = ifFalse.Compile(context, schema, requestContext, methodProvider);

        if (trueExp.Type != falseExp.Type)
            throw new EntityGraphQLCompilerException($"Conditional result types mismatch. Types '{trueExp.Type.Name}' and '{falseExp.Type.Name}' must be the same.");

        return Expression.Condition(condition.Compile(context, schema, requestContext, methodProvider), trueExp, falseExp);
    }
}
