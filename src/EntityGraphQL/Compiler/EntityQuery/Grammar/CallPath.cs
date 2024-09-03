using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.EntityQuery.Grammar;

internal sealed class CallPath(IReadOnlyList<IExpression> parts, CompileContext compileContext) : IExpression
{
    private readonly IReadOnlyList<IExpression> parts = parts;
    private readonly CompileContext compileContext = compileContext;

    public Type Type => parts[-1].Type;

    public Expression Compile(Expression? context, ISchemaProvider? schema, QueryRequestContext requestContext, IMethodProvider methodProvider)
    {
        if (parts.Count == 1)
        {
            return parts[0].Compile(context, schema, requestContext, methodProvider);
        }
        var exp = parts.Aggregate(
            context!,
            (currentContext, next) =>
            {
                if (next is CallExpression ce)
                {
                    return MakeMethodCall(schema, methodProvider, ref currentContext, ce.Name, ce.Arguments, requestContext);
                }
                if (next is IdentityExpression ie)
                {
                    return IdentityExpression.MakePropertyCall(currentContext!, schema, ie.Name, requestContext, compileContext);
                }
                return next.Compile(context, schema, requestContext, methodProvider);
            }
        );
        return exp;
    }

    internal static Expression MakeMethodCall(
        ISchemaProvider? schema,
        IMethodProvider methodProvider,
        ref Expression currentContext,
        string name,
        IReadOnlyList<IExpression>? arguments,
        QueryRequestContext requestContext
    )
    {
        if (currentContext == null)
            throw new EntityGraphQLCompilerException("CurrentContext is null");

        var method = name;
        if (!methodProvider.EntityTypeHasMethod(currentContext.Type, method))
        {
            throw new EntityGraphQLCompilerException($"Method '{method}' not found on current context '{currentContext.Type.Name}'");
        }
        // Keep the current context
        var outerContext = currentContext;
        // some methods might have a different inner context (IEnumerable etc)
        var methodArgContext = methodProvider.GetMethodContext(currentContext, method);
        currentContext = methodArgContext;
        // build our method call
        var localContext = currentContext; // Create a local variable to store the value of currentContext
        // Compile the arguments with the new context
        var args = arguments?.Select(a => a.Compile(localContext, schema, requestContext, methodProvider))?.ToList();
        var call = methodProvider.MakeCall(outerContext, methodArgContext, method, args, outerContext.Type);
        currentContext = call;
        return call;
    }
}
