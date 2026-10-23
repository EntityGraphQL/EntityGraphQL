using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.EntityQuery.Grammar;

internal sealed class CallPath(IReadOnlyList<IExpression> parts, EqlCompileContext compileContext) : IExpression
{
    private readonly IReadOnlyList<IExpression> parts = parts;
    private readonly EqlCompileContext compileContext = compileContext;

    public Type Type => parts[-1].Type;

    public Expression Compile(Expression? context, EntityQueryParser parser, ISchemaProvider? schema, QueryRequestContext requestContext, IMethodProvider methodProvider)
    {
        if (parts.Count == 1)
        {
            if (parts[0] is CallExpression ce)
            {
                return MakeMethodCall(schema, parser, methodProvider, ref context!, ce.Name, ce.Arguments, requestContext, compileContext);
            }
            if (parts[0] is IdentityExpression ie)
            {
                return IdentityExpression.MakePropertyCall(context!, schema, ie.Name, requestContext, compileContext);
            }
            return parts[0].Compile(context, parser, schema, requestContext, methodProvider);
        }
        var exp = parts.Aggregate(
            context!,
            (currentContext, next) =>
            {
                if (next is CallExpression ce)
                {
                    return MakeMethodCall(schema, parser, methodProvider, ref currentContext, ce.Name, ce.Arguments, requestContext, compileContext);
                }
                if (next is IdentityExpression ie)
                {
                    return IdentityExpression.MakePropertyCall(currentContext!, schema, ie.Name, requestContext, compileContext);
                }
                return next.Compile(context, parser, schema, requestContext, methodProvider);
            }
        );
        return exp;
    }

    internal static Expression MakeMethodCall(
        ISchemaProvider? schema,
        EntityQueryParser parser,
        IMethodProvider methodProvider,
        ref Expression currentContext,
        string name,
        IReadOnlyList<IExpression>? arguments,
        QueryRequestContext requestContext,
        EqlCompileContext compileContext
    )
    {
        if (currentContext == null)
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, "CurrentContext is null");

        var method = name;
        if (!methodProvider.EntityTypeHasMethod(currentContext.Type, method))
        {
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Method '{method}' not found on current context '{currentContext.Type.Name}'");
        }
        // Keep the current context
        var outerContext = currentContext;
        // some methods might have a different inner context (IEnumerable etc)
        var methodArgContext = methodProvider.GetMethodContext(currentContext, method);
        // A method on a collection (any, where...) has its element as the context of its arguments: of
        // the collection's schema type, where the collection's dotnet type could not say which.
        var collectionSchemaType = compileContext.ElementSchemaTypeOf(outerContext);
        if (collectionSchemaType != null && methodArgContext != outerContext)
            compileContext.RecordSchemaType(methodArgContext, collectionSchemaType, isList: false);
        currentContext = methodArgContext;
        // build our method call
        var localContext = currentContext; // Create a local variable to store the value of currentContext
        // Compile the arguments with the new context
        var args = arguments?.Select(a => a.Compile(localContext, parser, schema, requestContext, methodProvider))?.ToList();

        // Special handling for isAny: if the provided array/list element type doesn't match the context type,
        // convert the list elements to the context type using schema-aware converters first.
        if (string.Equals(method, "isAny", StringComparison.OrdinalIgnoreCase) && args != null && args.Count == 1)
        {
            var array = args[0];
            var arrayEleType = array.Type.GetEnumerableOrArrayType();
            if (arrayEleType != null)
            {
                var ctxType = outerContext.Type;
                // unwrap Nullable<T> for comparison
                var targetType = ctxType.IsNullableType() ? Nullable.GetUnderlyingType(ctxType)! : ctxType;
                if (arrayEleType != targetType)
                {
                    var p = Expression.Parameter(arrayEleType, "x");
                    var convertCall = Expression.Call(
                        typeof(ExpressionUtil),
                        nameof(ExpressionUtil.ConvertObjectType),
                        Type.EmptyTypes,
                        Expression.Convert(p, typeof(object)),
                        Expression.Constant(targetType, typeof(Type)),
                        Expression.Constant(schema, typeof(ISchemaProvider))
                    );
                    var body = Expression.Convert(convertCall, targetType);
                    var lambda = Expression.Lambda(body, p);

                    array = Expression.Call(array.Type.IsGenericTypeQueryable() ? typeof(Queryable) : typeof(Enumerable), nameof(Queryable.Select), [arrayEleType, targetType], array, lambda);

                    args[0] = array;
                }
            }
        }

        var call = methodProvider.MakeCall(outerContext, methodArgContext, method, args, outerContext.Type);
        // And so is what it returns, when that is the elements again - where(), first() - so a call
        // chained on it (where(...).any(...), first().score) still knows; any() and count() are not.
        if (collectionSchemaType != null)
        {
            if (GqlTypeInfo.IsListOf(call.Type, collectionSchemaType.TypeDotnet))
                compileContext.RecordSchemaType(call, collectionSchemaType, isList: true);
            // one of the elements - exactly their type - not a bool or a count, which an element type of
            // object would otherwise pass for
            else if (call.Type == methodArgContext.Type)
                compileContext.RecordSchemaType(call, collectionSchemaType, isList: false);
        }
        currentContext = call;
        return call;
    }
}
