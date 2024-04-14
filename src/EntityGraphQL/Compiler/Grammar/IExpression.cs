using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.Grammar;

internal interface IExpression
{
    Type Type { get; }
    Expression Compile(Expression? context, ISchemaProvider? schema, IMethodProvider methodProvider);
}

internal sealed class EqlExpression(Expression value) : IExpression
{
    private readonly Expression value = value;

    public Type Type => value.Type;

    public Expression Compile(Expression? context, ISchemaProvider? schema, IMethodProvider methodProvider)
    {
        return value;
    }
}

internal sealed class CallPath : IExpression
{
    private readonly List<IdentifierOrCall> parts;

    public CallPath(List<IdentifierOrCall> parts)
    {
        this.parts = parts;
    }

    public Type Type => parts.Last().Type;

    public Expression Compile(Expression? context, ISchemaProvider? schema, IMethodProvider methodProvider)
    {
        if (parts.Count == 1)
        {
            var name = parts[0].Name;
            try
            {
                return Expression.PropertyOrField(context!, name);
            }
            catch (Exception)
            {
                var enumField = schema!.GetEnumTypes()
                    .Select(e => e.GetFields().FirstOrDefault(f => f.Name == name))
                    .Where(f => f != null)
                    .FirstOrDefault();
                if (enumField != null)
                {
                    var constExp = Expression.Constant(Enum.Parse(enumField.ReturnType.TypeDotnet, enumField.Name));
                    if (constExp != null)
                        return constExp;
                }

                throw new EntityGraphQLCompilerException($"Field '{name}' not found on type '{schema?.GetSchemaType(context!.Type, null)?.Name ?? context!.Type.Name}'");
            }
        }
        var exp = parts.Aggregate(context!, (currentContext, next) =>
        {
            if (next is IdentifierOrCall id)
            {
                if (id.IsCall)
                {
                    if (currentContext == null)
                        throw new EntityGraphQLCompilerException("CurrentContext is null");

                    var method = id.Name;
                    if (!methodProvider.EntityTypeHasMethod(currentContext.Type, method))
                    {
                        throw new EntityGraphQLCompilerException($"Method '{method}' not found on current context '{currentContext.Type.Name}'");
                    }
                    // Keep the current context
                    var outerContext = currentContext;
                    // some methods might have a different inner context (IEnumerable etc)
                    var methodArgContext = methodProvider.GetMethodContext(currentContext, method);
                    currentContext = methodArgContext;
                    // Compile the arguments with the new context
                    var args = id.Arguments?.ToList();
                    // build our method call
                    var call = methodProvider.MakeCall(outerContext, methodArgContext, method, args?.Select(a => a.Compile(context, schema, methodProvider)), currentContext.Type);
                    currentContext = call;
                    return call;
                }
                else
                    return Expression.PropertyOrField(currentContext, id.Name);
            }
            throw new NotImplementedException();
        });
        return exp;
    }
}

internal sealed class ConditionExpression : IExpression
{
    private readonly IExpression condition;
    private readonly IExpression ifTrue;
    private readonly IExpression ifFalse;

    public ConditionExpression(IExpression condition, IExpression ifTrue, IExpression ifFalse)
    {
        this.condition = condition;
        this.ifTrue = ifTrue;
        this.ifFalse = ifFalse;
    }

    public Type Type => ifTrue.Type;

    public Expression Compile(Expression? context, ISchemaProvider? schema, IMethodProvider methodProvider)
    {
        return Expression.Condition(condition.Compile(context, schema, methodProvider), ifTrue.Compile(context, schema, methodProvider), ifFalse.Compile(context, schema, methodProvider));
    }
}
