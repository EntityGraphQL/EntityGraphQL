using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using EntityQueryLanguage.Grammer;
using EntityQueryLanguage.Extensions;

namespace EntityQueryLanguage
{
    internal class EqlGrammerVisitor : EqlGrammerBaseVisitor<Expression>
    {
        private Expression _currentContext;
        private ISchemaProvider _schemaProvider;
        private IMethodProvider _methodProvider;

        public EqlGrammerVisitor(Expression expression, ISchemaProvider schemaProvider, IMethodProvider methodProvider)
        {
            _currentContext = expression;
            _schemaProvider = schemaProvider;
            _methodProvider = methodProvider;
        }

        public override Expression VisitBinary(EqlGrammerParser.BinaryContext context)
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
                return Expression.Call(null, typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string) }), left, right);
            }

            return Expression.MakeBinary(op, left, right);
        }

        private Expression DoObjectComparisonOnDifferentTypes(ExpressionType op, Expression left, Expression right)
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
                
            return convertedToSameTypes ?  
                Expression.MakeBinary(op, left, right) : 
                null;
        }

        private static NewExpression ConvertToGuid(Expression expression)
        {
            // currently doesn't support nulls - to fix that wrap in  a conditional is null ? (Guid) null : below
            return Expression.New(typeof(Guid).GetConstructor(new Type[] { typeof(string) }), Expression.Call(expression, typeof(object).GetMethod("ToString")));
        }

        public override Expression VisitExpr(EqlGrammerParser.ExprContext context)
        {
            var r = Visit(context.body);
            return r;
        }

        public override Expression VisitCallPath(EqlGrammerParser.CallPathContext context)
        {
            var startingContext = _currentContext;
            Expression exp = null;
            foreach (var child in context.children)
            {
                var r = Visit(child);
                if (r == null)
                    continue;

                exp = r;
                _currentContext = exp;
            }
            _currentContext = startingContext;
            return exp;
        }

        public override Expression VisitIdentity(EqlGrammerParser.IdentityContext context)
        {
            // check that the schema has the property for the context
            var field = context.GetText();
            //TODO - need to get the mapped name for the type to check for fields to support mapped schema too
            if (!_schemaProvider.TypeHasField(_schemaProvider.GetSchemaTypeNameForRealType(_currentContext.Type), field))
            {
                throw new EqlCompilerException($"Field or property '{field}' not found on current context '{_currentContext.Type.Name}'");
            }
            var exp = _schemaProvider.GetExpressionForField(_currentContext, _currentContext.Type.Name, field);
            return exp;
        }

        public override Expression VisitInt(EqlGrammerParser.IntContext context)
        {
            return Expression.Constant(Int32.Parse(context.GetText()));
        }

        public override Expression VisitDecimal(EqlGrammerParser.DecimalContext context)
        {
            return Expression.Constant(Decimal.Parse(context.GetText()));
        }

        public override Expression VisitString(EqlGrammerParser.StringContext context)
        {
            return Expression.Constant(context.GetText().Trim('\''));
        }

        public override Expression VisitIfThenElse(EqlGrammerParser.IfThenElseContext context)
        {
            return Expression.Condition(CheckConditionalTest(Visit(context.test)), Visit(context.ifTrue), Visit(context.ifFalse));
        }

        public override Expression VisitIfThenElseInline(EqlGrammerParser.IfThenElseInlineContext context)
        {
            return Expression.Condition(CheckConditionalTest(Visit(context.test)), Visit(context.ifTrue), Visit(context.ifFalse));
        }

        public override Expression VisitCall(EqlGrammerParser.CallContext context)
        {
            var method = context.method.GetText();
            if (!_methodProvider.EntityTypeHasMethod(_currentContext.Type, method))
                throw new EqlCompilerException($"Method '{method}' not found on current context '{_currentContext.Type.Name}'");
            // Keep the current context
            var outerContext = _currentContext;
            // some methods might have a different inner context (IEnumerable etc)
            var methodArgContext = _methodProvider.GetMethodContext(_currentContext, method);
            _currentContext = methodArgContext;
            // Compile the arguments with the new context
            var args = context.arguments?.children.Select(c => Visit(c)).ToList();
            // build our method call
            var call = _methodProvider.MakeCall(outerContext, methodArgContext, method, args);
            _currentContext = call;
            return call;
        }

        public override Expression VisitArgs(EqlGrammerParser.ArgsContext context)
        {
            return VisitChildren(context);
        }
        //  public override Expression VisitCallOrId(EqlGrammerParser.CallOrIdContext context) {
        //    return VisitChildren(context);
        //  }
        //  public override Expression VisitConstant(EqlGrammerParser.ConstantContext context) {
        //    return VisitChildren(context);
        //  }
        //  public override Expression VisitOperator(EqlGrammerParser.OperatorContext context) {
        //    return VisitChildren(context);
        //  }
        //  public override Expression VisitExpression(EqlGrammerParser.ExpressionContext context) {
        //    return VisitChildren(context);
        //  }
        //  public override Expression VisitStartRule(EqlGrammerParser.StartRuleContext context) {
        //    Console.WriteLine($"----VisitStartRule {context.GetText()}");
        //    return VisitChildren(context);
        //  }

        /// Implements rules about comparing non-matching types.
        /// Nullable vs. non-nullable - the non-nullable gets converted to nullable
        /// int vs. uint - the uint gets down cast to int
        /// more to come...
        private Expression ConvertLeftOrRight(ExpressionType op, Expression left, Expression right)
        {
            if (left.Type.IsNullableType() && !right.Type.IsNullableType())
                right = Expression.Convert(right, left.Type);
            else if (right.Type.IsNullableType() && !left.Type.IsNullableType())
                left = Expression.Convert(left, right.Type);

            else if (left.Type == typeof(int) && right.Type == typeof(uint))
                right = Expression.Convert(right, left.Type);
            else if (left.Type == typeof(uint) && right.Type == typeof(int))
                left = Expression.Convert(left, right.Type);

            return Expression.MakeBinary(op, left, right);
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