using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using EntityQueryLanguage.Grammer;
using EntityQueryLanguage.Extensions;
using System.Collections.Generic;
using EntityQueryLanguage.Schema;
using System.Text.RegularExpressions;

namespace EntityQueryLanguage.Compiler
{

    internal class QueryGrammerNodeVisitor : EqlGrammerBaseVisitor<ExpressionResult>
    {
        private ExpressionResult currentContext;
        private ISchemaProvider schemaProvider;
        private IMethodProvider methodProvider;
        private Field fieldArgumentContext;
        private Regex guidRegex = new Regex(@"^[0-9A-F]{8}[-]?([0-9A-F]{4}[-]?){3}[0-9A-F]{12}$", RegexOptions.IgnoreCase);

        public QueryGrammerNodeVisitor(Expression expression, ISchemaProvider schemaProvider, IMethodProvider methodProvider)
        {
            currentContext = (ExpressionResult)expression;
            this.schemaProvider = schemaProvider;
            this.methodProvider = methodProvider;
        }

        public override ExpressionResult VisitBinary(EqlGrammerParser.BinaryContext context)
        {
            var left = Visit(context.left);
            var right = Visit(context.right);
            var op = MakeOperator(context.op.GetText());
            // we may need to do some converting here
            if (left.Type != right.Type)
            {
                if (op == ExpressionType.Equal)
                {
                    var result = DoObjectComparisonOnDifferentTypes(op, left, right);

                    if (result != null)
                        return result;
                }
                return ConvertLeftOrRight(op, left, right);
            }

            if (op == ExpressionType.Add && left.Type == typeof(string) && right.Type == typeof(string))
            {
                return (ExpressionResult)Expression.Call(null, typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string) }), left, right);
            }

            return (ExpressionResult)Expression.MakeBinary(op, left, right);
        }

        private ExpressionResult DoObjectComparisonOnDifferentTypes(ExpressionType op, ExpressionResult left, ExpressionResult right)
        {
            var convertedToSameTypes = false;

            // leftGuid == 'asdasd' == null ? (Guid) null : new Guid('asdasdas'.ToString())
            // leftGuid == null
            if (left.Type == typeof(Guid) && right.Type != typeof(Guid))
            {
                right = ConvertToGuid(right);
                convertedToSameTypes = true;
            }
            else if (right.Type == typeof(Guid) && left.Type != typeof(Guid))
            {
                left = ConvertToGuid(left);
                convertedToSameTypes = true;
            }

            return convertedToSameTypes ? (ExpressionResult)Expression.MakeBinary(op, left, right) : null;
        }

        private static ExpressionResult ConvertToGuid(ExpressionResult expression)
        {
            return (ExpressionResult)Expression.Call(typeof(Guid), "Parse", null, (ExpressionResult)Expression.Call(expression, typeof(object).GetMethod("ToString")));
        }

        public override ExpressionResult VisitExpr(EqlGrammerParser.ExprContext context)
        {
            var r = Visit(context.body);
            return r;
        }

        public override ExpressionResult VisitCallPath(EqlGrammerParser.CallPathContext context)
        {
            var startingContext = currentContext;
            ExpressionResult exp = null;
            foreach (var child in context.children)
            {
                var r = Visit(child);
                if (r == null)
                    continue;

                exp = r;
                currentContext = exp;
            }
            currentContext = startingContext;
            return exp;
        }

        public override ExpressionResult VisitIdentity(EqlGrammerParser.IdentityContext context)
        {
            var field = context.GetText();
            return MakeFieldExpression(field, null);
        }

        public override ExpressionResult VisitGqlcall(EqlGrammerParser.GqlcallContext context)
        {
            var field = context.method.GetText();
            var args = context.gqlarguments.children.Cast<EqlGrammerParser.GqlargContext>().ToDictionary(a => a.gqlfield.GetText().ToLower(), a => {
                fieldArgumentContext = schemaProvider.GetFieldType(currentContext, field);
                var r = VisitGqlarg(a);
                fieldArgumentContext = null;
                return r;
            });
            return MakeFieldExpression(field, args);
        }

        public override ExpressionResult VisitGqlarg(EqlGrammerParser.GqlargContext context)
        {
            var enumName = context.gqlvalue.GetText();
            var argType = fieldArgumentContext.GetArgumentType(context.gqlfield.GetText());
            if (!argType.GetTypeInfo().IsEnum)
            {
                // could be a constant or some other compilable expression
                return Visit(context.gqlvalue);
            }
            var valueIndex = Enum.GetNames(argType).ToList().FindIndex(n => n.ToLower() == enumName.ToLower());
            if (valueIndex == -1)
            {
                throw new EqlCompilerException($"Value {enumName} is not valid for argument {context.gqlfield}");
            }
            var enumValue = Enum.GetValues(argType).GetValue(valueIndex);
            return (ExpressionResult)Expression.Constant(enumValue);
        }

        private ExpressionResult MakeFieldExpression(string field, Dictionary<string, ExpressionResult> args)
        {
            // check that the schema has the property for the context
            // TODO - need to get the mapped name for the type to check for fields to support mapped schema too
            if (!schemaProvider.TypeHasField(schemaProvider.GetSchemaTypeNameForRealType(currentContext.Type), field))
            {
                throw new EqlCompilerException($"Field or property '{field}' not found on current context '{currentContext.Type.Name}'");
            }
            var exp = schemaProvider.GetExpressionForField(currentContext, currentContext.Type.Name, field, args);
            return exp;
        }

        public override ExpressionResult VisitInt(EqlGrammerParser.IntContext context)
        {
            return (ExpressionResult)Expression.Constant(Int32.Parse(context.GetText()));
        }

        public override ExpressionResult VisitDecimal(EqlGrammerParser.DecimalContext context)
        {
            return (ExpressionResult)Expression.Constant(Decimal.Parse(context.GetText()));
        }

        public override ExpressionResult VisitString(EqlGrammerParser.StringContext context)
        {
            // we may need to convert a string into a DateTime or Guid type
            string value = context.GetText().Trim('\'');
            var exp = (ExpressionResult)Expression.Constant(value);
            if (guidRegex.IsMatch(value))
                exp = ConvertToGuid(exp);
            return exp;
        }

        public override ExpressionResult VisitIfThenElse(EqlGrammerParser.IfThenElseContext context)
        {
            return (ExpressionResult)Expression.Condition(CheckConditionalTest(Visit(context.test)), Visit(context.ifTrue), Visit(context.ifFalse));
        }

        public override ExpressionResult VisitIfThenElseInline(EqlGrammerParser.IfThenElseInlineContext context)
        {
            return (ExpressionResult)Expression.Condition(CheckConditionalTest(Visit(context.test)), Visit(context.ifTrue), Visit(context.ifFalse));
        }

        public override ExpressionResult VisitCall(EqlGrammerParser.CallContext context)
        {
            var method = context.method.GetText();
            if (!methodProvider.EntityTypeHasMethod(currentContext.Type, method))
            {
                throw new EqlCompilerException($"Method '{method}' not found on current context '{currentContext.Type.Name}'");
            }
            // Keep the current context
            var outerContext = currentContext;
            // some methods might have a different inner context (IEnumerable etc)
            var methodArgContext = methodProvider.GetMethodContext(currentContext, method);
            currentContext = methodArgContext;
            // Compile the arguments with the new context
            var args = context.arguments?.children.Select(c => Visit(c)).ToList();
            // build our method call
            var call = methodProvider.MakeCall(outerContext, methodArgContext, method, args);
            currentContext = call;
            return call;
        }

        public override ExpressionResult VisitArgs(EqlGrammerParser.ArgsContext context)
        {
            return VisitChildren(context);
        }

        /// Implements rules about comparing non-matching types.
        /// Nullable vs. non-nullable - the non-nullable gets converted to nullable
        /// int vs. uint - the uint gets down cast to int
        /// more to come...
        private ExpressionResult ConvertLeftOrRight(ExpressionType op, ExpressionResult left, ExpressionResult right)
        {
            if (left.Type.IsNullableType() && !right.Type.IsNullableType())
                right = (ExpressionResult)Expression.Convert(right, left.Type);
            else if (right.Type.IsNullableType() && !left.Type.IsNullableType())
                left = (ExpressionResult)Expression.Convert(left, right.Type);

            else if (left.Type == typeof(int) && right.Type == typeof(uint))
                right = (ExpressionResult)Expression.Convert(right, left.Type);
            else if (left.Type == typeof(uint) && right.Type == typeof(int))
                left = (ExpressionResult)Expression.Convert(left, right.Type);

            return (ExpressionResult)Expression.MakeBinary(op, left, right);
        }

        private Expression CheckConditionalTest(Expression test)
        {
            if (test.Type != typeof(bool))
                throw new EqlCompilerException($"Expected boolean value in conditional test but found '{test}'");
            return test;
        }

        private ExpressionType MakeOperator(string op)
        {
            switch (op)
            {
                case "=": return ExpressionType.Equal;
                case "+": return ExpressionType.Add;
                case "-": return ExpressionType.Subtract;
                case "%": return ExpressionType.Modulo;
                case "^": return ExpressionType.Power;
                case "and": return ExpressionType.AndAlso;
                case "*": return ExpressionType.Multiply;
                case "or": return ExpressionType.OrElse;
                case "<=": return ExpressionType.LessThanOrEqual;
                case ">=": return ExpressionType.GreaterThanOrEqual;
                case "<": return ExpressionType.LessThan;
                case ">": return ExpressionType.GreaterThan;
                default: throw new EqlCompilerException($"Unsupported binary operator '{op}'");
            }
        }
    }
}