using System;
using System.Linq.Expressions;
using EntityQL.Grammer;
using System.Globalization;
using EntityGraphQL.Schema;
using System.Linq;
using Antlr4.Runtime.Misc;
using System.Text;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Compiles a constant value from a query into an Expression. null if the constant is a unknown identifier (ENUM)
    /// </summary>
    internal sealed class ConstantVisitor : EntityQLBaseVisitor<Expression?>
    {
        private readonly ISchemaProvider? schema;

        public ConstantVisitor(ISchemaProvider? schema)
        {
            this.schema = schema;
        }

        public override Expression? VisitConstant([NotNull] EntityQLParser.ConstantContext context)
        {
            if (context.stringVal != null)
            {
                // we may need to convert a string into a DateTime or Guid type
                // this will happen later in the use of this requires that type
                var sb = new StringBuilder(context.stringVal.Text[1..^1]);
                sb
                    .Replace("\\'", "\'")
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\")
                    .Replace("\\0", "\0")
                    .Replace("\\a", "\a")
                    .Replace("\\b", "\b")
                    .Replace("\\f", "\f")
                    .Replace("\\n", "\n")
                    .Replace("\\r", "\r")
                    .Replace("\\t", "\t")
                    .Replace("\\v", "\v");
                return Expression.Constant(sb.ToString());
            }

            return base.VisitConstant(context);
        }

        public override Expression? VisitInt(EntityQLParser.IntContext context)
        {
            string s = context.GetText();
            return Expression.Constant(long.Parse(s, CultureInfo.InvariantCulture));
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