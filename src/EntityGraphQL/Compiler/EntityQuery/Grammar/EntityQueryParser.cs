using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace EntityGraphQL.Compiler.EntityQuery.Grammar;

public sealed class EntityQueryParser
{
    public static readonly EntityQueryParser Instance;
    private const string MultiplyChar = "*";
    private const string DivideChar = "/";
    private const string ModChar = "%";
    private const string PlusChar = "+";
    private const string MinusChar = "-";
    private const string LessThanOrEqualStr = "<=";
    private const string GreaterThanOrEqualStr = ">=";
    private const string LessThanChar = "<";
    private const string GreaterThanChar = ">";
    private const string EqualStr = "==";
    private const string NotEqualStr = "!=";
    private const string PowerChar = "^";
    private const string AndWord = "and";
    private const string AndStr = "&&";
    private const string OrWord = "or";
    private const string OrStr = "||";
    private readonly Parser<IExpression> grammar;

    private static readonly Parser<string> multiply = Terms.Text(MultiplyChar);
    private static readonly Parser<string> divide = Terms.Text(DivideChar);
    private static readonly Parser<string> mod = Terms.Text(ModChar);
    private static readonly Parser<string> plus = Terms.Text(PlusChar);
    private static readonly Parser<string> minus = Terms.Text(MinusChar);
    private static readonly Parser<string> power = Terms.Text(PowerChar);

    private static readonly Parser<string> lessThanOrEqual = Terms.Text(LessThanOrEqualStr);
    private static readonly Parser<string> greaterThanOrEqual = Terms.Text(GreaterThanOrEqualStr);
    private static readonly Parser<string> lessThan = Terms.Text(LessThanChar);
    private static readonly Parser<string> greaterThan = Terms.Text(GreaterThanChar);
    private static readonly Parser<string> equals = Terms.Text(EqualStr);
    private static readonly Parser<string> notEquals = Terms.Text(NotEqualStr);
    private static readonly Parser<string> andWord = Terms.Text(AndWord);
    private static readonly Parser<string> andSymbol = Terms.Text(AndStr);
    private static readonly Parser<string> orWord = Terms.Text(OrWord);
    private static readonly Parser<string> orSymbol = Terms.Text(OrStr);

    private static readonly Parser<char> openParen = Terms.Char('(');
    private static readonly Parser<char> closeParen = Terms.Char(')');
    private static readonly Parser<char> openArray = Terms.Char('[');
    private static readonly Parser<char> closeArray = Terms.Char(']');
    private static readonly Parser<char> dot = Terms.Char('.');
    private static readonly Parser<char> comma = Terms.Char(',');
    private static readonly Parser<char> questionMark = Terms.Char('?');
    private static readonly Parser<char> colon = Terms.Char(':');
    private static readonly Parser<char> dollarSign = Terms.Char('$');
    private static readonly Parser<string> ifExp = Terms.Text("if");
    private static readonly Parser<string> thenExp = Terms.Text("then");
    private static readonly Parser<string> elseExp = Terms.Text("else");

    private static readonly Parser<IExpression> numberExp = Terms
        .Number<decimal>(NumberOptions.AllowLeadingSign | NumberOptions.Float)
        .Then<IExpression>(static d =>
        {
#if NET7_0_OR_GREATER
            var scale = d.Scale;
#else
            var bits = decimal.GetBits(d);
            var scale = (int)((bits[3] >> 16) & 0x7F);
#endif
            if (scale != 0)
                return new EqlExpression(Expression.Constant(d));

            if (d >= short.MinValue && d <= short.MaxValue)
                return new EqlExpression(Expression.Constant((short)d));

            if (d >= int.MinValue && d <= int.MaxValue)
                return new EqlExpression(Expression.Constant((int)d));

            return new EqlExpression(Expression.Constant((long)d));
        });

    private static readonly Parser<IExpression> strExp = SkipWhiteSpace(new StringLiteral(StringLiteralQuotes.SingleOrDouble))
        .Then<IExpression>(static s => new EqlExpression(Expression.Constant(s.ToString())));

    // Variable expression: $variableName
    private static readonly Parser<IExpression> variableExp = dollarSign
        .And(new Identifier())
        .Then<IExpression>(static (c, x) => new VariableExpression(x.Item2.ToString()!, ((EntityQueryParseContext)c).CompileContext));

    private EntityQueryParser()
    {
        // The Deferred helper creates a parser that can be referenced by others before it is defined
        var expression = Deferred<IExpression>();

        // "(" expression ")"
        var groupExpression = Between(openParen, expression, closeParen);

        var callArgs = openParen.And(Separated(comma, expression)).And(closeParen).Then(static x => x.Item2);
        var emptyCallArgs = openParen.And(closeParen).Then(static x => new List<IExpression>() as IReadOnlyList<IExpression>);

        var identifier = SkipWhiteSpace(new Identifier())
            .And(Not(emptyCallArgs))
            .Then<IExpression>(static (c, x) => new IdentityExpression(x.Item1.ToString()!, ((EntityQueryParseContext)c).CompileContext));

        var constArray = openArray
            .And(Separated(comma, expression))
            .And(closeArray)
            .Then<IExpression>(
                static (c, x) =>
                {
                    var firstType = x.Item2[0].Type;
                    var promotedType = IsIntegralNumericType(firstType) ? PromoteIntegralNumericType(x.Item2.Select(expression => expression.Type).ToArray()) : firstType;

                    return new EqlExpression(
                        Expression.NewArrayInit(
                            promotedType,
                            x.Item2.Select(e =>
                                e.Type != promotedType
                                    ? Expression.Convert(
                                        e.Compile(
                                            ((EntityQueryParseContext)c).Context,
                                            Instance,
                                            ((EntityQueryParseContext)c).Schema,
                                            ((EntityQueryParseContext)c).RequestContext,
                                            ((EntityQueryParseContext)c).MethodProvider
                                        ),
                                        promotedType
                                    )
                                    : e.Compile(
                                        ((EntityQueryParseContext)c).Context,
                                        Instance,
                                        ((EntityQueryParseContext)c).Schema,
                                        ((EntityQueryParseContext)c).RequestContext,
                                        ((EntityQueryParseContext)c).MethodProvider
                                    )
                            )
                        )
                    );
                }
            );

        var call = SkipWhiteSpace(new Identifier()).And(callArgs.Or(emptyCallArgs)).Then<IExpression>(static x => new CallExpression(x.Item1!.ToString()!, x.Item2));

        var callPath = Separated(dot, OneOf(call, constArray, identifier)).Then<IExpression>(static (c, p) => new CallPath(p, ((EntityQueryParseContext)c).CompileContext));

        var nullExp = Terms.Text("null").AndSkip(Not(identifier)).Then<IExpression>(static _ => new EqlExpression(Expression.Constant(null)));
        var trueExp = Terms.Text("true").AndSkip(Not(identifier)).Then<IExpression>(static _ => new EqlExpression(Expression.Constant(true)));
        var falseExp = Terms.Text("false").AndSkip(Not(identifier)).Then<IExpression>(static _ => new EqlExpression(Expression.Constant(false)));

        // primary => NUMBER | "(" expression ")";
        var primary = numberExp.Or(strExp).Or(trueExp).Or(falseExp).Or(nullExp).Or(variableExp).Or(callPath).Or(groupExpression).Or(constArray);

        // The Recursive helper allows to create parsers that depend on themselves.
        // ( "-" ) unary | primary;
        var unary = Recursive<IExpression>(
            (u) =>
                minus
                    .And(u)
                    .Then<IExpression>(
                        static (c, x) =>
                            new EqlExpression(
                                Expression.Negate(
                                    x.Item2.Compile(
                                        ((EntityQueryParseContext)c).Context,
                                        Instance,
                                        ((EntityQueryParseContext)c).Schema,
                                        ((EntityQueryParseContext)c).RequestContext,
                                        ((EntityQueryParseContext)c).MethodProvider
                                    )
                                )
                            )
                    )
                    .Or(primary)
        );

        // factor => unary ( ( "*" | "/" | ... ) unary )* ;
        var mathOps = OneOf(multiply, divide, mod, plus, minus, power);
        var mathExp = unary.And(ZeroOrMany(mathOps.And(unary))).Then(HandleBinary);

        // expression => mathExp ( ( "==" | "&&" | ... ) mathExp )* ;
        var compareOps = OneOf(lessThanOrEqual, greaterThanOrEqual, lessThan, greaterThan, equals, notEquals);
        var compareExp = mathExp.And(ZeroOrMany(compareOps.And(mathExp))).Then(HandleBinary);

        var logicalOps = OneOf(andWord, andSymbol, orWord, orSymbol);
        var logicalBinary = compareExp.And(ZeroOrMany(logicalOps.And(compareExp))).Then(HandleBinary);

        var conditional = OneOf(
            logicalBinary
                .And(questionMark)
                .And(logicalBinary)
                .And(colon)
                .And(logicalBinary)
                .Then(static d =>
                {
                    var condition = d.Item1;
                    var trueExp = d.Item3;
                    var falseExp = d.Item5;
                    return (IExpression)new ConditionExpression(condition, trueExp, falseExp);
                }),
            ifExp
                .And(logicalBinary)
                .And(thenExp)
                .And(logicalBinary)
                .And(elseExp)
                .And(logicalBinary)
                .Then(static d =>
                {
                    var condition = d.Item2;
                    var trueExp = d.Item4;
                    var falseExp = d.Item6;
                    return (IExpression)new ConditionExpression(condition, trueExp, falseExp);
                })
        );

        expression.Parser = conditional.Or(logicalBinary);

        grammar = expression;
    }

    private static bool IsIntegralNumericType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        return underlyingType == typeof(short) || underlyingType == typeof(int) || underlyingType == typeof(long);
    }

    private static Type PromoteIntegralNumericType(Type[] types)
    {
        bool hasLong = types.Contains(typeof(long));
        bool hasInt = types.Contains(typeof(int));

        if (hasLong)
            return typeof(long);
        if (hasInt)
            return typeof(int);

        return types[0];
    }

    static EntityQueryParser()
    {
        Instance = new EntityQueryParser();
    }

    private static IExpression HandleBinary((IExpression, IReadOnlyList<(string, IExpression)>) x)
    {
        var left = x.Item1;
        var binaryExp = left;

        foreach (var item in x.Item2)
        {
            var opStr = item.Item1;
            var right = item.Item2;

            var op = opStr.ToString() switch
            {
                MultiplyChar => ExpressionType.Multiply,
                DivideChar => ExpressionType.Divide,
                ModChar => ExpressionType.Modulo,
                PlusChar => ExpressionType.Add,
                MinusChar => ExpressionType.Subtract,
                LessThanOrEqualStr => ExpressionType.LessThanOrEqual,
                GreaterThanOrEqualStr => ExpressionType.GreaterThanOrEqual,
                LessThanChar => ExpressionType.LessThan,
                GreaterThanChar => ExpressionType.GreaterThan,
                EqualStr => ExpressionType.Equal,
                NotEqualStr => ExpressionType.NotEqual,
                PowerChar => ExpressionType.Power,
                AndWord => ExpressionType.AndAlso,
                AndStr => ExpressionType.AndAlso,
                OrWord => ExpressionType.OrElse,
                OrStr => ExpressionType.OrElse,
                _ => throw new NotSupportedException(),
            };
            binaryExp = new Binary(op, left, right);

            left = binaryExp;
        }
        return binaryExp;
    }

    private sealed class EntityQueryParseContext : ParseContext
    {
        public EntityQueryParseContext(string query, Expression? context, ISchemaProvider? schema, QueryRequestContext requestContext, IMethodProvider methodProvider, EqlCompileContext compileContext)
            : base(new Parlot.Scanner(query))
        {
            Context = context;
            Schema = schema;
            RequestContext = requestContext;
            MethodProvider = methodProvider;
            CompileContext = compileContext;
        }

        public Expression? Context { get; }
        public ISchemaProvider? Schema { get; }
        public QueryRequestContext RequestContext { get; }
        public IMethodProvider MethodProvider { get; }
        public EqlCompileContext CompileContext { get; }
    }

    public Expression Parse(string query, Expression? context, ISchemaProvider? schema, QueryRequestContext requestContext, IMethodProvider methodProvider, EqlCompileContext compileContext)
    {
        var parseContext = new EntityQueryParseContext(query, context, schema, requestContext, methodProvider, compileContext);

        var result = grammar.Parse(parseContext) ?? throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, "Failed to parse query");
        return result.Compile(parseContext.Context, Instance, parseContext.Schema, parseContext.RequestContext, parseContext.MethodProvider);
    }
}
