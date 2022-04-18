using System;
using System.Linq.Expressions;
using EntityQL.Grammer;
using System.Text.RegularExpressions;
using System.Globalization;
using EntityGraphQL.Schema;
using System.Linq;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Compiles a constant value from a query into an Expression. null if the constant is a unknown identifier (ENUM)
    /// </summary>
    internal class ConstantVisitor : EntityQLBaseVisitor<Expression?>
    {
        public static readonly Regex GuidRegex = new(@"^[0-9A-F]{8}[-]?([0-9A-F]{4}[-]?){3}[0-9A-F]{12}$", RegexOptions.IgnoreCase);
        public static readonly Regex DateTimeRegex = new("^[0-9]{4}[-][0-9]{2}[-][0-9]{2}([T ][0-9]{2}[:][0-9]{2}[:][0-9]{2}(\\.[0-9]{1,7})?([-+][0-9]{3})?)?$", RegexOptions.IgnoreCase);
        private readonly ISchemaProvider? schema;

        public ConstantVisitor(ISchemaProvider? schema)
        {
            this.schema = schema;
        }

        public override Expression? VisitInt(EntityQLParser.IntContext context)
        {
            string s = context.GetText();
            return Expression.Constant(long.Parse(s));
        }

        public override Expression? VisitBoolean(EntityQLParser.BooleanContext context)
        {
            string s = context.GetText();
            return Expression.Constant(bool.Parse(s));
        }

        public override Expression? VisitDecimal(EntityQLParser.DecimalContext context)
        {
            return Expression.Constant(decimal.Parse(context.GetText(), CultureInfo.InvariantCulture));
        }

        public override Expression? VisitString(EntityQLParser.StringContext context)
        {
            // we may need to convert a string into a DateTime or Guid type
            string value = context.GetText()[1..^1].Replace("\\\"", "\"");
            if (GuidRegex.IsMatch(value))
                return Expression.Constant(Guid.Parse(value));

            if (DateTimeRegex.IsMatch(value))
                return Expression.Constant(DateTime.Parse(value));

            return Expression.Constant(value);
        }

        public override Expression? VisitNull(EntityQLParser.NullContext context)
        {
            var exp = Expression.Constant(null);
            return exp;
        }

        public override Expression? VisitIdentity(EntityQLParser.IdentityContext context)
        {
            if (schema == null)
                throw new InvalidOperationException("Schema is not set");
            // this should be an enum
            var enumVal = context.GetText();
            var enumField = schema.GetEnumTypes()
                .Select(e => e.GetFields().FirstOrDefault(f => f.Name == enumVal))
                .Where(f => f != null)
                .FirstOrDefault();
            if (enumField == null)
                return null;

            var exp = Expression.Constant(Enum.Parse(enumField.ReturnType.TypeDotnet, enumField.Name));
            return exp;
        }
    }
}