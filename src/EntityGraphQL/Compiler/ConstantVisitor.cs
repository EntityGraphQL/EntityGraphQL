using System;
using System.Linq.Expressions;
using EntityGraphQL.Grammer;
using System.Text.RegularExpressions;
using System.Globalization;
using EntityGraphQL.Schema;
using System.Linq;

namespace EntityGraphQL.Compiler
{

    internal class ConstantVisitor : EntityGraphQLBaseVisitor<ExpressionResult>
    {
        public static readonly Regex GuidRegex = new Regex(@"^[0-9A-F]{8}[-]?([0-9A-F]{4}[-]?){3}[0-9A-F]{12}$", RegexOptions.IgnoreCase);
        public static readonly Regex DateTimeRegex = new Regex("^[0-9]{4}[-][0-9]{2}[-][0-9]{2}([T ][0-9]{2}[:][0-9]{2}[:][0-9]{2}(\\.[0-9]{1,7})?([-+][0-9]{3})?)?$", RegexOptions.IgnoreCase);
        private readonly ISchemaProvider schema;

        public ConstantVisitor(ISchemaProvider schema)
        {
            this.schema = schema;
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

            if (DateTimeRegex.IsMatch(value))
                return (ExpressionResult)Expression.Constant(DateTime.Parse(value));

            return (ExpressionResult)Expression.Constant(value);
        }

        public override ExpressionResult VisitNull(EntityGraphQLParser.NullContext context)
        {
            var exp = (ExpressionResult)Expression.Constant(null);
            return exp;
        }

        public override ExpressionResult VisitIdentity(EntityGraphQLParser.IdentityContext context)
        {
            // this should be an enum
            var enumVal = context.GetText();
            var enumField = schema.EnumTypes()
                .Select(e => e.GetFields().FirstOrDefault(f => f.Name == enumVal))
                .Where(f => f != null)
                .FirstOrDefault();
            if (enumField == null)
                return null;

            var exp = (ExpressionResult)Expression.Constant(Enum.Parse(enumField.ReturnType.TypeDotnet, enumField.Name));
            return exp;
        }
    }
}