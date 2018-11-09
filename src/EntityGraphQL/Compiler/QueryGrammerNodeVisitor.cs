using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using EntityGraphQL.Grammer;
using EntityGraphQL.Extensions;
using System.Collections.Generic;
using EntityGraphQL.Schema;
using System.Text.RegularExpressions;
using EntityGraphQL.LinqQuery;

namespace EntityGraphQL.Compiler
{

    internal class QueryGrammerNodeVisitor : EntityGraphQLBaseVisitor<ExpressionResult>
    {
        private ExpressionResult currentContext;
        private ISchemaProvider schemaProvider;
        private IMethodProvider methodProvider;
        private readonly QueryVariables variables;
        private IMethodType fieldArgumentContext;
        private Regex guidRegex = new Regex(@"^[0-9A-F]{8}[-]?([0-9A-F]{4}[-]?){3}[0-9A-F]{12}$", RegexOptions.IgnoreCase);

        public QueryGrammerNodeVisitor(Expression expression, ISchemaProvider schemaProvider, IMethodProvider methodProvider, QueryVariables variables)
        {
            currentContext = (ExpressionResult)expression;
            this.schemaProvider = schemaProvider;
            this.methodProvider = methodProvider;
            this.variables = variables;
        }

        public override ExpressionResult VisitBinary(EntityGraphQLParser.BinaryContext context)
        {
            var left = Visit(context.left);
            var right = Visit(context.right);
            var op = MakeOperator(context.op.GetText());
            // we may need to do some converting here
            if (left.Type != right.Type)
            {
                if (op == ExpressionType.Equal || op == ExpressionType.NotEqual)
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

        public override ExpressionResult VisitExpr(EntityGraphQLParser.ExprContext context)
        {
            var r = Visit(context.body);
            return r;
        }

        public override ExpressionResult VisitCallPath(EntityGraphQLParser.CallPathContext context)
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

        public override ExpressionResult VisitIdentity(EntityGraphQLParser.IdentityContext context)
        {
            var field = context.GetText();
            return MakeFieldExpression(field, null);
        }

        public override ExpressionResult VisitGqlcall(EntityGraphQLParser.GqlcallContext context)
        {
            var method = context.method.GetText();
            IMethodType methodType = schemaProvider.GetMethodType(currentContext, method);
            var args = context.gqlarguments.children.Where(c => c.GetType() == typeof(EntityGraphQLParser.GqlargContext)).Cast<EntityGraphQLParser.GqlargContext>().ToDictionary(a => a.gqlfield.GetText().ToLower(), a => {
                fieldArgumentContext = methodType;
                var r = VisitGqlarg(a);
                fieldArgumentContext = null;
                return r;
            }, StringComparer.OrdinalIgnoreCase);
            if (schemaProvider.HasMutation(method))
            {
                return MakeMutationExpression(method, (MutationType)methodType, args);
            }
            return MakeFieldExpression(method, args);
        }

        public override ExpressionResult VisitGqlarg(EntityGraphQLParser.GqlargContext context)
        {
            if (context.gqlVar() != null)
            {
                string varKey = context.gqlVar().GetText().TrimStart('$');
                object value = variables.GetValueFor(varKey);
                var exp = (ExpressionResult)Expression.Constant(value);
                if (value != null && value.GetType() == typeof(string) && guidRegex.IsMatch((string)value))
                    exp = ConvertToGuid(exp);
                return exp;
            }
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
                throw new EntityGraphQLCompilerException($"Value {enumName} is not valid for argument {context.gqlfield}");
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
                throw new EntityGraphQLCompilerException($"Field or property '{field}' not found on current context '{currentContext.Type.Name}'");
            }
            var exp = schemaProvider.GetExpressionForField(currentContext, currentContext.Type.Name, field, args);
            return exp;
        }

        private ExpressionResult MakeMutationExpression(string method, MutationType mutationType, Dictionary<string, ExpressionResult> args)
        {
            return new MutationResult(method, mutationType, args);
        }

        public override ExpressionResult VisitInt(EntityGraphQLParser.IntContext context)
        {
            return (ExpressionResult)Expression.Constant(Int32.Parse(context.GetText()));
        }

        public override ExpressionResult VisitDecimal(EntityGraphQLParser.DecimalContext context)
        {
            return (ExpressionResult)Expression.Constant(Decimal.Parse(context.GetText()));
        }

        public override ExpressionResult VisitString(EntityGraphQLParser.StringContext context)
        {
            // we may need to convert a string into a DateTime or Guid type
            string value = context.GetText().Trim('\'');
            var exp = (ExpressionResult)Expression.Constant(value);
            if (guidRegex.IsMatch(value))
                exp = ConvertToGuid(exp);
            return exp;
        }

        public override ExpressionResult VisitIfThenElse(EntityGraphQLParser.IfThenElseContext context)
        {
            return (ExpressionResult)Expression.Condition(CheckConditionalTest(Visit(context.test)), Visit(context.ifTrue), Visit(context.ifFalse));
        }

        public override ExpressionResult VisitIfThenElseInline(EntityGraphQLParser.IfThenElseInlineContext context)
        {
            return (ExpressionResult)Expression.Condition(CheckConditionalTest(Visit(context.test)), Visit(context.ifTrue), Visit(context.ifFalse));
        }

        public override ExpressionResult VisitCall(EntityGraphQLParser.CallContext context)
        {
            var method = context.method.GetText();
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
            var args = context.arguments?.children.Select(c => Visit(c)).ToList();
            // build our method call
            var call = methodProvider.MakeCall(outerContext, methodArgContext, method, args);
            currentContext = call;
            return call;
        }

        public override ExpressionResult VisitArgs(EntityGraphQLParser.ArgsContext context)
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
                throw new EntityGraphQLCompilerException($"Expected boolean value in conditional test but found '{test}'");
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
                default: throw new EntityGraphQLCompilerException($"Unsupported binary operator '{op}'");
            }
        }
    }
}