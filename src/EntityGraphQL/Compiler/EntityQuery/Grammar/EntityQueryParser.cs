using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace EntityGraphQL.Compiler.EntityQuery.Grammar;

public sealed class EntityQueryParser
{
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
    private static readonly Parser<string> ifExp = Terms.Text("if");
    private static readonly Parser<string> thenExp = Terms.Text("then");
    private static readonly Parser<string> elseExp = Terms.Text("else");

    private static readonly Parser<IExpression> longExp = Terms.Integer(NumberOptions.AllowLeadingSign).Then<IExpression>(static d => new EqlExpression(Expression.Constant(d)));

    // decimal point is required otherwise we want a long
    private static readonly Parser<IExpression> decimalExp = Terms
        .Integer(NumberOptions.AllowLeadingSign)
        .And(dot)
        .And(Terms.Integer(NumberOptions.None))
        .Then<IExpression>(static d => new EqlExpression(Expression.Constant(decimal.Parse($"{d.Item1}.{d.Item3}", NumberStyles.Number, CultureInfo.InvariantCulture))));
    private static readonly Parser<IExpression> nullExp = Terms.Text("null").Then<IExpression>(static _ => new EqlExpression(Expression.Constant(null)));
    private static readonly Parser<IExpression> trueExp = Terms.Text("true").Then<IExpression>(static _ => new EqlExpression(Expression.Constant(true)));
    private static readonly Parser<IExpression> falseExp = Terms.Text("false").Then<IExpression>(static _ => new EqlExpression(Expression.Constant(false)));
    private static readonly Parser<IExpression> strExp = SkipWhiteSpace(new StringLiteral(StringLiteralQuotes.SingleOrDouble))
        .Then<IExpression>(static s => new EqlExpression(Expression.Constant(s.ToString())));
    private readonly Expression? context;
    private readonly ISchemaProvider? schema;
    private readonly QueryRequestContext requestContext;
    private readonly IMethodProvider methodProvider;

    public EntityQueryParser(Expression? context, ISchemaProvider? schema, QueryRequestContext requestContext, IMethodProvider methodProvider, CompileContext compileContext)
    {
        // The Deferred helper creates a parser that can be referenced by others before it is defined
        var expression = Deferred<IExpression>();

        // "(" expression ")"
        var groupExpression = Between(openParen, expression, closeParen);

        var callArgs = openParen.And(Separated(comma, expression)).And(closeParen).Then(static x => x.Item2);
        var emptyCallArgs = openParen.And(closeParen).Then(static x => new List<IExpression>() as IReadOnlyList<IExpression>);

        var identifier = SkipWhiteSpace(new Identifier()).And(Not(emptyCallArgs)).Then<IExpression>(x => new IdentityExpression(x.Item1.ToString()!, compileContext));

        var constArray = openArray
            .And(Separated(comma, expression))
            .And(closeArray)
            .Then<IExpression>(x => new EqlExpression(Expression.NewArrayInit(x.Item2[0].Type, x.Item2.Select(e => e.Compile(context, schema, requestContext, methodProvider)))));

        var call = SkipWhiteSpace(new Identifier()).And(callArgs.Or(emptyCallArgs)).Then<IExpression>(static x => new CallExpression(x.Item1!.ToString()!, x.Item2));

        var callPath = Separated(dot, OneOf(call, constArray, identifier)).Then<IExpression>(p => new CallPath(p, compileContext));

        // primary => NUMBER | "(" expression ")";
        var primary = decimalExp.Or(longExp).Or(strExp).Or(trueExp).Or(falseExp).Or(nullExp).Or(callPath).Or(groupExpression).Or(constArray);

        // The Recursive helper allows to create parsers that depend on themselves.
        // ( "-" ) unary | primary;
        var unary = Recursive<IExpression>(
            (u) => minus.And(u).Then<IExpression>(x => new EqlExpression(Expression.Negate(x.Item2.Compile(context, schema, requestContext, methodProvider)))).Or(primary)
        );

        // factor => unary ( ( "*" | "/" | ... ) unary )* ;
        var mathOps = OneOf(multiply, divide, mod, plus, minus, power);
        var mathExp = unary.And(ZeroOrMany(mathOps.And(unary))).Then((x) => HandleBinary(x, context));

        // expression => mathExp ( ( "==" | "&&" | ... ) mathExp )* ;
        var compareOps = OneOf(lessThanOrEqual, greaterThanOrEqual, lessThan, greaterThan, equals, notEquals);
        var compareExp = mathExp.And(ZeroOrMany(compareOps.And(mathExp))).Then((x) => HandleBinary(x, context));

        var logicalOps = OneOf(andWord, andSymbol, orWord, orSymbol);
        var logicalBinary = compareExp.And(ZeroOrMany(logicalOps.And(compareExp))).Then((x) => HandleBinary(x, context));

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
        this.context = context;
        this.schema = schema;
        this.requestContext = requestContext;
        this.methodProvider = methodProvider;
    }

    private static IExpression HandleBinary((IExpression, IReadOnlyList<(string, IExpression)>) x, Expression? context)
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
                OrWord => ExpressionType.Or,
                OrStr => ExpressionType.Or,
                _ => throw new NotSupportedException()
            };
            binaryExp = new Binary(op, left, right);

            left = binaryExp;
        }
        return binaryExp;
    }

    public Expression Parse(string query)
    {
        var result = grammar.Parse(query) ?? throw new EntityGraphQLCompilerException("Failed to parse query");
        return result.Compile(context, schema, requestContext, methodProvider);
    }
}
