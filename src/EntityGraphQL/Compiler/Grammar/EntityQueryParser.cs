using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using Parlot;
using Parlot.Compilation;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace EntityGraphQL.Compiler.Grammar;

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
    private readonly Parser<Expression> grammar;

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
    private static readonly Parser<char> dot = Terms.Char('.');
    private static readonly Parser<char> comma = Terms.Char(',');
    private static readonly Parser<char> questionMark = Terms.Char('?');
    private static readonly Parser<char> colon = Terms.Char(':');
    private static readonly Parser<string> ifExp = Terms.Text("if");
    private static readonly Parser<string> thenExp = Terms.Text("then");
    private static readonly Parser<string> elseExp = Terms.Text("else");

    private static readonly Parser<Expression> longExp = Terms.Integer(NumberOptions.AllowSign)
        .Then<Expression>(static d => Expression.Constant(d));
    // decimal point is required otherwise we want a long
    private static readonly Parser<Expression> decimalExp = Terms.Integer(NumberOptions.AllowSign).And(dot).And(Terms.Integer(NumberOptions.None))
        .Then<Expression>(static d => Expression.Constant(decimal.Parse($"{d.Item1}.{d.Item3}", System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture)));
    private static readonly Parser<Expression> nullExp = Terms.Text("null")
        .Then<Expression>(static _ => Expression.Constant(null));
    private static readonly Parser<Expression> trueExp = Terms.Text("true")
        .Then<Expression>(static _ => Expression.Constant(true));
    private static readonly Parser<Expression> falseExp = Terms.Text("false")
        .Then<Expression>(static _ => Expression.Constant(false));
    private static readonly Parser<Expression> strExp = Terms.String(StringLiteralQuotes.SingleOrDouble)
        .Then<Expression>(static s => Expression.Constant(s.ToString()));

    public EntityQueryParser(Expression? context, ISchemaProvider? schema, QueryRequestContext requestContext, IMethodProvider methodProvider)
    {
        // The Deferred helper creates a parser that can be referenced by others before it is defined
        var expression = Deferred<Expression>();

        // "(" expression ")"
        var groupExpression = Between(openParen, expression, closeParen);

        var identifier = SkipWhiteSpace(new EqlIdentifier())
            .Then(x => new IdentifierPart(x.ToString()));
        var call = SkipWhiteSpace(new EqlIdentifier()).And(openParen).And(Separated(comma, expression)).And(closeParen)
            .Then(x => new IdentifierPart(x.Item1.ToString(), x.Item3.ToList()));

        var callPath = Separated(dot, OneOf(call, identifier))
            .Then(d =>
            {
                var exp = d.Aggregate(context!, (currentContext, next) =>
                {
                    var nextField = next;
                    try
                    {
                        if (nextField.IsCall)
                        {
                            if (currentContext == null)
                                throw new EntityGraphQLCompilerException("CurrentContext is null");

                            var method = nextField.Name;
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
                            var args = nextField.Arguments?.ToList();
                            // build our method call
                            var call = methodProvider.MakeCall(outerContext, methodArgContext, method, args, currentContext.Type);
                            currentContext = call;
                            return call;
                        }
                        else
                            return Expression.PropertyOrField(currentContext, nextField.Name);
                    }
                    catch (Exception)
                    {
                        var enumField = schema!.GetEnumTypes()
                            .Select(e => e.GetFields().FirstOrDefault(f => f.Name == nextField.Name))
                            .Where(f => f != null)
                            .FirstOrDefault();
                        if (enumField != null)
                        {
                            var exp = Expression.Constant(Enum.Parse(enumField.ReturnType.TypeDotnet, enumField.Name));
                            if (exp != null)
                                return exp;
                        }

                        throw new EntityGraphQLCompilerException($"Field '{next}' not found on type '{schema?.GetSchemaType(currentContext.Type, null)?.Name ?? currentContext.Type.Name}'");
                    }
                });
                return exp;
            });

        // primary => NUMBER | "(" expression ")";
        var primary = decimalExp.Or(longExp).Or(strExp).Or(trueExp).Or(falseExp).Or(nullExp).Or(groupExpression).Or(callPath);

        // The Recursive helper allows to create parsers that depend on themselves.
        // ( "-" ) unary | primary;
        var unary = Recursive<Expression>((u) =>
            minus.And(u)
                .Then<Expression>(static x => Expression.Negate(x.Item2))
                .Or(primary));

        // factor => unary ( ( "*" | "/" | ... ) unary )* ;
        var mathOps = OneOf(multiply, divide, mod,
                            plus, minus,
                            power);
        var mathExp = unary.And(ZeroOrMany(mathOps.And(unary)))
            .Then(HandleBinary);

        // expression => mathExp ( ( "==" | "&&" | ... ) mathExp )* ;
        var logicalOps = OneOf(lessThanOrEqual, greaterThanOrEqual,
                            lessThan, greaterThan,
                            equals, notEquals,
                            andWord, andSymbol,
                            orWord, orSymbol);
        var logicalBinary = mathExp.And(ZeroOrMany(logicalOps.And(mathExp)))
            .Then(HandleBinary);

        var conditional = OneOf(
            logicalBinary.And(questionMark).And(logicalBinary).And(colon).And(logicalBinary)
                .Then(d =>
                {
                    var condition = d.Item1;
                    var trueExp = d.Item3;
                    var falseExp = d.Item5;
                    if (trueExp.Type != falseExp.Type)
                        throw new EntityGraphQLCompilerException($"Conditional result types mismatch. Types '{trueExp.Type.Name}' and '{falseExp.Type.Name}' must be the same.");
                    return (Expression)Expression.Condition(condition, trueExp, falseExp);
                }),
            ifExp.And(logicalBinary).And(thenExp).And(logicalBinary).And(elseExp).And(logicalBinary)
                .Then(d =>
                {
                    var condition = d.Item2;
                    var trueExp = d.Item4;
                    var falseExp = d.Item6;
                    if (trueExp.Type != falseExp.Type)
                        throw new EntityGraphQLCompilerException($"Conditional result types mismatch. Types '{trueExp.Type.Name}' and '{falseExp.Type.Name}' must be the same.");
                    return (Expression)Expression.Condition(condition, trueExp, falseExp);
                })
            );

        expression.Parser = conditional.Or(logicalBinary);

        grammar = expression;
    }

    private static Expression HandleBinary((Expression, List<(string, Expression)>) x)
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
                AndWord => ExpressionType.And,
                AndStr => ExpressionType.And,
                OrWord => ExpressionType.Or,
                OrStr => ExpressionType.Or,
                _ => throw new NotSupportedException()
            };

            if (left.Type != right.Type)
            {
                // if (op == ExpressionType.Equal || op == ExpressionType.NotEqual)
                // {
                //     var result = DoObjectComparisonOnDifferentTypes(op, left, right);

                //     if (result != null)
                //         binaryExp = result;
                // }
                binaryExp = ConvertLeftOrRight(op, left, right);
            }
            else
            {
                binaryExp = Expression.MakeBinary(op, left, right);
            }

            left = binaryExp;
        }
        return binaryExp;
    }

    private static MethodCallExpression ConvertToDateTime(Expression expression)
    {
        return Expression.Call(typeof(DateTime), nameof(DateTime.Parse), null, expression, Expression.Constant(CultureInfo.InvariantCulture));
    }
    private static MethodCallExpression ConvertToGuid(Expression expression)
    {
        return Expression.Call(typeof(Guid), nameof(Guid.Parse), null, Expression.Call(expression, typeof(object).GetMethod(nameof(ToString))!));
    }
    private static BinaryExpression ConvertLeftOrRight(ExpressionType op, Expression left, Expression right)
    {
        if (left.Type.IsNullableType() && !right.Type.IsNullableType())
            right = Expression.Convert(right, right.Type.GetNullableType());
        else if (right.Type.IsNullableType() && !left.Type.IsNullableType())
            left = Expression.Convert(left, left.Type.GetNullableType());

        else if (left.Type == typeof(int) && (right.Type == typeof(uint) || right.Type == typeof(short) || right.Type == typeof(long) || right.Type == typeof(ushort) || right.Type == typeof(ulong)))
            right = Expression.Convert(right, left.Type);
        else if (left.Type == typeof(uint) && (right.Type == typeof(int) || right.Type == typeof(short) || right.Type == typeof(long) || right.Type == typeof(ushort) || right.Type == typeof(ulong)))
            left = Expression.Convert(left, right.Type);

        if (left.Type != right.Type)
        {
            if (left.Type.IsEnum && right.Type.IsEnum)
                throw new EntityGraphQLCompilerException($"Cannot compare enums of different types '{left.Type.Name}' and '{right.Type.Name}'");
            if (left.Type == typeof(Guid) || left.Type == typeof(Guid?) && right.Type == typeof(string))
                right = ConvertToGuid(right);
            else if (right.Type == typeof(Guid) || right.Type == typeof(Guid?) && left.Type == typeof(string))
                left = ConvertToGuid(left);
            else if (left.Type == typeof(DateTime) || left.Type == typeof(DateTime?) && right.Type == typeof(string))
                right = ConvertToDateTime(right);
            else if (right.Type == typeof(DateTime) || right.Type == typeof(DateTime?) && left.Type == typeof(string))
                left = ConvertToDateTime(left);
            // convert ints "up" to float/decimal
            else if ((left.Type == typeof(int) || left.Type == typeof(uint) || left.Type == typeof(short) || left.Type == typeof(ushort) || left.Type == typeof(long) || left.Type == typeof(ulong)) &&
                    (right.Type == typeof(float) || right.Type == typeof(double) || right.Type == typeof(decimal)))
                left = Expression.Convert(left, right.Type);
            else if ((right.Type == typeof(int) || right.Type == typeof(uint) || right.Type == typeof(short) || right.Type == typeof(ushort) || right.Type == typeof(long) || right.Type == typeof(ulong)) &&
                    (left.Type == typeof(float) || left.Type == typeof(double) || left.Type == typeof(decimal)))
                right = Expression.Convert(right, left.Type);
            else // default try to make types match
                left = Expression.Convert(left, right.Type);
        }

        if (left.Type.IsNullableType() && !right.Type.IsNullableType())
            right = Expression.Convert(right, left.Type);
        else if (right.Type.IsNullableType() && !left.Type.IsNullableType())
            left = Expression.Convert(left, right.Type);

        return Expression.MakeBinary(op, left, right);
    }

    public Expression Parse(string query)
    {
        var result = grammar.Parse(query);
        return result;
    }
}

internal sealed class IdentifierPart
{
    private List<Expression>? arguments;

    public IdentifierPart(string name)
    {
        this.Name = name;
    }

    public IdentifierPart(string v, List<Expression> arguments) : this(v)
    {
        this.arguments = arguments;
    }

    public bool IsCall => arguments != null;

    public string Name { get; }
    public IEnumerable<Expression>? Arguments => arguments;
}

/// <summary>
/// From Parlot.Fluent.Identifier so I can ignore keywords.
/// Better way? Hopefully
/// </summary>
internal sealed class EqlIdentifier : Parser<TextSpan>, ICompilable
{
    private static readonly HashSet<string> keywords = [
        "if",
    ];

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var first = context.Scanner.Cursor.Current;

        if (Character.IsIdentifierStart(first))
        {
            var start = context.Scanner.Cursor.Offset;

            // At this point we have an identifier, read while it's an identifier part.

            context.Scanner.Cursor.AdvanceNoNewLines(1);

            while (!context.Scanner.Cursor.Eof && Character.IsIdentifierPart(context.Scanner.Cursor.Current))
            {
                context.Scanner.Cursor.AdvanceNoNewLines(1);
            }

            var end = context.Scanner.Cursor.Offset;

            result.Set(start, end, new TextSpan(context.Scanner.Buffer, start, end - start));
            if (keywords.Contains(result.Value.ToString()))
                return false;
            return true;
        }

        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = new CompilationResult();

        var success = context.DeclareSuccessVariable(result, false);
        var value = context.DeclareValueVariable(result, Expression.Default(typeof(TextSpan)));

        var first = Expression.Parameter(typeof(char), $"first{context.NextNumber}");
        result.Body.Add(Expression.Assign(first, context.Current()));
        result.Variables.Add(first);

        var start = Expression.Parameter(typeof(int), $"start{context.NextNumber}");

        var breakLabel = Expression.Label($"break_{context.NextNumber}");

        var block = Expression.Block(
            Expression.IfThen(
                Expression.OrElse(
                    Expression.Call(typeof(Character).GetMethod(nameof(Character.IsIdentifierStart))!, first),
                    Expression.Constant(false, typeof(bool))
                        ),
                Expression.Block(
                    [start],
                    Expression.Assign(start, context.Offset()),
                    context.AdvanceNoNewLine(Expression.Constant(1)),
                    Expression.Loop(
                        Expression.IfThenElse(
                            /* if */ Expression.AndAlso(
                                Expression.Not(context.Eof()),
                                    Expression.OrElse(
                                        Expression.Call(typeof(Character).GetMethod(nameof(Character.IsIdentifierPart))!, context.Current()),
                                        Expression.Constant(false, typeof(bool))
                                        )
                                ),
                            /* then */ context.AdvanceNoNewLine(Expression.Constant(1)),
                            /* else */ Expression.Break(breakLabel)
                            ),
                        breakLabel
                        ),
                    context.DiscardResult
                        ? Expression.Empty()
                        : Expression.Assign(value, context.NewTextSpan(context.Buffer(), start, Expression.Subtract(context.Offset(), start))),
                    Expression.Assign(success, Expression.Constant(true, typeof(bool)))
                )
            )
        );

        result.Body.Add(block);

        return result;
    }
}