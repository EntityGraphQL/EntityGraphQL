using System;
using System.Linq.Expressions;
using EntityGraphQL.Grammer;
using System.Text.RegularExpressions;
using System.Globalization;

namespace EntityGraphQL.Compiler
{

    internal class ConstantVisitor : EntityGraphQLBaseVisitor<ExpressionResult>
    {
        public static readonly Regex GuidRegex = new Regex(@"^[0-9A-F]{8}[-]?([0-9A-F]{4}[-]?){3}[0-9A-F]{12}$", RegexOptions.IgnoreCase);

        public ConstantVisitor()
        {
        }

        public override ExpressionResult VisitInt(EntityGraphQLParser.IntContext context)
        {
            string s = context.GetText();
            return (ExpressionResult)(s.StartsWith("-") ? Expression.Constant(Int64.Parse(s)) : Expression.Constant(UInt64.Parse(s)));
        }

        public override ExpressionResult VisitBoolean(EntityGraphQLParser.BooleanContext context)
        {
            string s = context.GetText();
            return (ExpressionResult)Expression.Constant(bool.Parse(s));
        }

        public override ExpressionResult VisitDecimal(EntityGraphQLParser.DecimalContext context)
        {
            return (ExpressionResult)Expression.Constant(Decimal.Parse(context.GetText(), CultureInfo.InvariantCulture));
        }

        public override ExpressionResult VisitString(EntityGraphQLParser.StringContext context)
        {
            // we may need to convert a string into a DateTime or Guid type
            string value = context.GetText().Substring(1, context.GetText().Length - 2).Replace("\\\"", "\"");
            if (GuidRegex.IsMatch(value))
                return (ExpressionResult)Expression.Constant(Guid.Parse(value));

            return (ExpressionResult)Expression.Constant(value);
        }

        public override ExpressionResult VisitNull(EntityGraphQLParser.NullContext context)
        {
            // we may need to convert a string into a DateTime or Guid type
            var exp = (ExpressionResult)Expression.Constant(null);
            return exp;
        }
    }
}