using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.EntityQuery.Grammar;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.EntityQuery;

/// Simple language to write queries against an object schema.
///
/// myEntity.where(field = 'value')
///
///   (primary_key) - e.g. myEntity(12)
/// Binary Operators
///   =, !=, <, <=, >, >=, +, -, *, %, /, in
/// Unary Operators
///   not(), !
public static class EntityQueryCompiler
{
    private static readonly Dictionary<Type, Func<Expression, Expression>> literalParsers = new();

    public static CompiledQueryResult Compile(string query, EqlCompileContext compileContext)
    {
        return Compile(query, null, compileContext, null);
    }

    public static CompiledQueryResult Compile(string query, ISchemaProvider? schemaProvider, CompileContext compileContext, IMethodProvider? methodProvider = null) =>
        Compile(query, schemaProvider, new EqlCompileContext(compileContext), methodProvider);

    /// <summary>
    /// Compile a query.
    /// </summary>
    /// <param name="query">The query text</param>
    /// <param name="schemaProvider"></param>
    /// <param name="methodProvider"></param>
    /// <returns></returns>
    public static CompiledQueryResult Compile(string query, ISchemaProvider? schemaProvider, EqlCompileContext compileContext, IMethodProvider? methodProvider = null)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(query, nameof(query));
#else
        if (query == null)
            throw new ArgumentNullException(nameof(query));
#endif

        ParameterExpression? contextParam = null;

        methodProvider ??= new EqlMethodProvider();

        if (schemaProvider != null)
            contextParam = Expression.Parameter(schemaProvider.QueryContextType, $"cxt_{schemaProvider.QueryContextType.Name}");

        var expression = CompileQuery(query, contextParam, schemaProvider, new QueryRequestContext(null, null), methodProvider, compileContext);

        var contextParams = new List<ParameterExpression>();
        if (contextParam != null)
            contextParams.Add(contextParam);
        return new CompiledQueryResult(expression, contextParams);
    }

    public static CompiledQueryResult CompileWith(
        string query,
        Expression context,
        ISchemaProvider schemaProvider,
        QueryRequestContext requestContext,
        EqlCompileContext compileContext,
        IMethodProvider? methodProvider = null
    )
    {
        methodProvider ??= new EqlMethodProvider();
        var expression =
            CompileQuery(query, context, schemaProvider, requestContext, methodProvider, compileContext)
            ?? throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, "Failed to compile expression");
        var parameters = expression.NodeType == ExpressionType.Lambda ? ((LambdaExpression)expression).Parameters.ToList() : [];
        return new CompiledQueryResult(expression, parameters);
    }

    private static Expression CompileQuery(
        string query,
        Expression? context,
        ISchemaProvider? schemaProvider,
        QueryRequestContext requestContext,
        IMethodProvider methodProvider,
        EqlCompileContext compileContext
    )
    {
        var expression = EntityQueryParser.Instance.Parse(query, context, schemaProvider, requestContext, methodProvider, literalParsers, compileContext);
        return expression;
    }

    /// <summary>
    /// Registers a parser for binary comparisons that converts a string operand to TTarget at compile time.
    /// Applied when one side is string and the other is TTarget or Nullable<TTarget>. Works for string literals and string-typed variables.
    /// </summary>
    /// <typeparam name="TTarget">Target type.</typeparam>
    /// <param name="makeParseExpression">Factory that turns a string Expression into a TTarget Expression.</param>
    /// <returns>The schema provider.</returns>
    public static void RegisterLiteralParser<TTarget>(Func<Expression, Expression> makeParseExpression)
    {
        literalParsers[typeof(TTarget)] = makeParseExpression;
    }
}
