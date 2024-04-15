using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.Grammar;

internal sealed class CallPath(List<IdentifierOrCall> parts) : IExpression
{
    private readonly List<IdentifierOrCall> parts = parts;

    public Type Type => parts.Last().Type;

    public Expression Compile(Expression? context, ISchemaProvider? schema, IMethodProvider methodProvider)
    {
        if (parts.Count == 1)
        {
            var name = parts[0].Name;
            return MakePropertyCall(context!, schema, name, methodProvider);
        }
        var exp = parts.Aggregate(context!, (currentContext, next) =>
        {
            if (next is IdentifierOrCall id)
            {
                if (id.IsCall)
                {
                    return MakeMethodCall(schema, methodProvider, ref currentContext, id.Name, id.Arguments);
                }
                else
                    return MakePropertyCall(currentContext!, schema, id.Name, methodProvider);
            }
            throw new NotImplementedException();
        });
        return exp;
    }

    private static Expression MakeMethodCall(ISchemaProvider? schema, IMethodProvider methodProvider, ref Expression currentContext, string name, List<IExpression>? arguments)
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
        // Compile the arguments with the new context
        var args = arguments?.ToList();
        // build our method call
        var localContext = currentContext; // Create a local variable to store the value of currentContext
        var call = methodProvider.MakeCall(outerContext, methodArgContext, method, args?.Select(a => a.Compile(localContext, schema, methodProvider)), currentContext.Type);
        currentContext = call;
        return call;
    }

    private static Expression MakePropertyCall(Expression context, ISchemaProvider? schema, string name, IMethodProvider methodProvider)
    {
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
            // Check for a method because the parser is not always catching empty ()
            if (methodProvider.EntityTypeHasMethod(context.Type, name))
            {
                return MakeMethodCall(schema, methodProvider, ref context, name, []);
            }

            throw new EntityGraphQLCompilerException($"Field '{name}' not found on type '{schema?.GetSchemaType(context!.Type, null)?.Name ?? context!.Type.Name}'");
        }
    }
}
