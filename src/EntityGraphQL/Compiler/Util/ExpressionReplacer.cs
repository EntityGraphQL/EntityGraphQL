using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler.Util
{
    /// <summary>
    /// Used to replace whole expressions in a service field expression
    /// </summary>
    public class ExpressionReplacer : ExpressionVisitor
    {
        private readonly Expression newContext;
        private readonly Dictionary<Expression, GraphQLExtractedField> expressionsToReplace = new();

        public ExpressionReplacer(IEnumerable<GraphQLExtractedField> expressionsToReplace, Expression newContext)
        {
            this.newContext = newContext;
            foreach (var field in expressionsToReplace)
            {
                foreach (var exp in field.FieldExpressions)
                {
                    this.expressionsToReplace.Add(exp, field);
                }
            }
        }

        public Expression Replace(Expression baseExpression)
        {
            return base.Visit(baseExpression);
        }


        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (expressionsToReplace.ContainsKey(node))
                return expressionsToReplace[node].GetNodeExpression(newContext);
            return base.VisitParameter(node);
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            if (expressionsToReplace.ContainsKey(node))
                return expressionsToReplace[node].GetNodeExpression(newContext);
            return base.VisitLambda(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (expressionsToReplace.ContainsKey(node))
                return expressionsToReplace[node].GetNodeExpression(newContext);
            return base.VisitUnary(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (expressionsToReplace.ContainsKey(node))
                return expressionsToReplace[node].GetNodeExpression(newContext);
            return base.VisitMember(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (expressionsToReplace.ContainsKey(node))
                return expressionsToReplace[node].GetNodeExpression(newContext);
            return base.VisitConstant(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (expressionsToReplace.ContainsKey(node))
                return expressionsToReplace[node].GetNodeExpression(newContext);
            return base.VisitMethodCall(node);
        }
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (expressionsToReplace.ContainsKey(node))
                return expressionsToReplace[node].GetNodeExpression(newContext);
            return base.VisitBinary(node);
        }
    }
}