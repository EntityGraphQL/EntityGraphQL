using System;
using System.Linq;
using System.Linq.Expressions;
using EntityQL.Grammer;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using System.Collections.Generic;

namespace EntityGraphQL.Compiler.EntityQuery
{
    internal class EntityQueryNodeVisitor : EntityQLBaseVisitor<ExpressionResult>
    {
        private readonly QueryRequestContext requestContext;
        private ExpressionResult? currentContext;
        private readonly ISchemaProvider? schemaProvider;
        private readonly IMethodProvider methodProvider;
        private readonly ConstantVisitor constantVisitor;

        public EntityQueryNodeVisitor(Expression? expression, ISchemaProvider? schemaProvider, IMethodProvider methodProvider, QueryRequestContext requestContext)
        {
            this.requestContext = requestContext;
            currentContext = expression == null ? null : new ExpressionResult(expression);
            this.schemaProvider = schemaProvider;
            this.methodProvider = methodProvider;
            this.constantVisitor = new ConstantVisitor(schemaProvider);
        }

        public override ExpressionResult VisitBinary(EntityQLParser.BinaryContext context)
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

        private ExpressionResult? DoObjectComparisonOnDifferentTypes(ExpressionType op, ExpressionResult left, ExpressionResult right)
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

            var result = convertedToSameTypes ? (ExpressionResult)Expression.MakeBinary(op, left, right) : null;
            return result;
        }

        private static ExpressionResult ConvertToGuid(ExpressionResult expression)
        {
            return (ExpressionResult)Expression.Call(typeof(Guid), "Parse", null, (ExpressionResult)Expression.Call(expression, typeof(object).GetMethod("ToString")));
        }

        public override ExpressionResult VisitExpr(EntityQLParser.ExprContext context)
        {
            var r = Visit(context.body);
            return r;
        }

        public override ExpressionResult VisitCallPath(EntityQLParser.CallPathContext context)
        {
            var startingContext = currentContext;
            ExpressionResult? exp = null;
            foreach (var child in context.children)
            {
                var r = Visit(child);
                if (r == null)
                    continue;

                if (exp != null)
                {
                    r.AddServices(exp.Services);
                }
                exp = r;
                currentContext = exp;
            }
            currentContext = startingContext;
            if (exp == null)
                throw new EntityGraphQLCompilerException($"Could not compile expression for {context.GetText()}");
            return exp;
        }

        public override ExpressionResult VisitIdentity(EntityQLParser.IdentityContext context)
        {
            if (schemaProvider == null)
                throw new EntityGraphQLCompilerException("SchemaProvider is null");
            if (currentContext == null)
                throw new EntityGraphQLCompilerException("CurrentContext is null");

            var field = context.GetText();
            var schemaType = schemaProvider.GetSchemaType(currentContext.Type, requestContext);
            if (!schemaType.HasField(field, requestContext))
            {
                var enumOrConstantValue = constantVisitor.Visit(context);
                if (enumOrConstantValue == null)
                    throw new EntityGraphQLCompilerException($"Field {field} not found on type {schemaType.Name}");
                return enumOrConstantValue;
            }
            var gqlField = schemaType.GetField(field, requestContext);
            var exp = gqlField.GetExpression(gqlField.ResolveExpression!, currentContext, null, null, new Dictionary<string, object>(), null, null, new List<GraphQLDirective>(), false);
            return exp!;
        }

        public override ExpressionResult VisitConstant(EntityQLParser.ConstantContext context)
        {
            var result = constantVisitor.VisitConstant(context);
            if (result == null)
                throw new EntityGraphQLCompilerException($"Could not compile constant {context.GetText()}");
            return result;
        }


        public override ExpressionResult VisitIfThenElse(EntityQLParser.IfThenElseContext context)
        {
            return (ExpressionResult)Expression.Condition(CheckConditionalTest(Visit(context.test)), Visit(context.ifTrue), Visit(context.ifFalse));
        }

        public override ExpressionResult VisitIfThenElseInline(EntityQLParser.IfThenElseInlineContext context)
        {
            return (ExpressionResult)Expression.Condition(CheckConditionalTest(Visit(context.test)), Visit(context.ifTrue), Visit(context.ifFalse));
        }

        public override ExpressionResult VisitCall(EntityQLParser.CallContext context)
        {
            if (currentContext == null)
                throw new EntityGraphQLCompilerException("CurrentContext is null");

            var method = context.method.GetText();
            if (!methodProvider.EntityTypeHasMethod(currentContext.Type, method))
            {
                throw new EntityGraphQLCompilerException($"Method '{method}' not found on current context '{currentContext.Type.Name}'");
            }
            // Keep the current context
            var outerContext = currentContext;
            // some methods might have a different inner context (IEnumerable etc)
            var methodArgContext = methodProvider.GetMethodContext(currentContext, method);
            currentContext = (ExpressionResult)methodArgContext;
            // Compile the arguments with the new context
            var args = context.arguments?.children.Select(c => Visit(c)).ToList();
            // build our method call
            var call = (ExpressionResult)methodProvider.MakeCall(outerContext, methodArgContext, method, args?.Select(e => e.Expression));
            currentContext = call;
            return call;
        }

        public override ExpressionResult VisitArgs(EntityQLParser.ArgsContext context)
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

            else if (left.Type == typeof(int) && (right.Type == typeof(uint) || right.Type == typeof(Int16) || right.Type == typeof(Int64) || right.Type == typeof(UInt16) || right.Type == typeof(UInt64)))
                right = (ExpressionResult)Expression.Convert(right, left.Type);
            else if (left.Type == typeof(uint) && (right.Type == typeof(int) || right.Type == typeof(Int16) || right.Type == typeof(Int64) || right.Type == typeof(UInt16) || right.Type == typeof(UInt64)))
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
            return op switch
            {
                "==" => ExpressionType.Equal,
                "+" => ExpressionType.Add,
                "-" => ExpressionType.Subtract,
                "/" => ExpressionType.Divide,
                "%" => ExpressionType.Modulo,
                "^" => ExpressionType.Power,
                "and" => ExpressionType.AndAlso,
                "&&" => ExpressionType.AndAlso,
                "*" => ExpressionType.Multiply,
                "or" => ExpressionType.OrElse,
                "||" => ExpressionType.OrElse,
                "<=" => ExpressionType.LessThanOrEqual,
                ">=" => ExpressionType.GreaterThanOrEqual,
                "<" => ExpressionType.LessThan,
                ">" => ExpressionType.GreaterThan,
                _ => throw new EntityGraphQLCompilerException($"Unsupported binary operator '{op}'"),
            };
        }
    }
}