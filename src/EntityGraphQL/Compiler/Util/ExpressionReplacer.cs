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
        private readonly bool replaceInline;
        private readonly bool replaceWithNewContext;
        private readonly Dictionary<Expression, GraphQLExtractedField> expressionsToReplace = new();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="expressionsToReplace"></param>
        /// <param name="newContext"></param>
        /// <param name="replaceInline">If true, the matched expression is replaced. If false it will be rebuilt with potential a new name</param>
        public ExpressionReplacer(IEnumerable<GraphQLExtractedField> expressionsToReplace, Expression newContext, bool replaceInline, bool replaceWithNewContext)
        {
            this.newContext = newContext;
            this.replaceInline = replaceInline;
            this.replaceWithNewContext = replaceWithNewContext;
            foreach (var field in expressionsToReplace)
            {
                foreach (var exp in field.FieldExpressions)
                {
                    this.expressionsToReplace.Add(exp, field);
                }
            }
        }

        /// <summary>
        /// Visit the baseExpression and replace any expressions that match the expressionsToReplace with the newContext
        /// </summary>
        /// <param name="baseExpression">Expression to visit and look for matching expressions</param>
        /// <returns></returns>
        public Expression Replace(Expression baseExpression)
        {
            return base.Visit(baseExpression);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (expressionsToReplace.ContainsKey(node))
            {
                if (replaceInline || replaceWithNewContext)
                    return newContext;
                return expressionsToReplace[node].GetNodeExpression(newContext);
            }
            return base.VisitParameter(node);
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            if (expressionsToReplace.ContainsKey(node))
            {
                if (replaceInline || replaceWithNewContext)
                    return newContext;
                return expressionsToReplace[node].GetNodeExpression(newContext);
            }
            return base.VisitLambda(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (expressionsToReplace.ContainsKey(node))
            {
                if (replaceInline || replaceWithNewContext)
                    return newContext;
                return expressionsToReplace[node].GetNodeExpression(newContext);
            }
            return base.VisitUnary(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (expressionsToReplace.ContainsKey(node))
            {
                if (replaceInline)
                    return Expression.PropertyOrField(newContext, node.Member.Name);
                if (replaceWithNewContext)
                    return newContext;
                return expressionsToReplace[node].GetNodeExpression(newContext);
            }
            return base.VisitMember(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (expressionsToReplace.ContainsKey(node))
            {
                if (replaceInline || replaceWithNewContext)
                    return newContext;
                return expressionsToReplace[node].GetNodeExpression(newContext);
            }
            return base.VisitConstant(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (expressionsToReplace.ContainsKey(node))
            {
                if (replaceInline)
                    return Expression.Call(newContext, node.Method);
                if (replaceWithNewContext)
                    return newContext;
                return expressionsToReplace[node].GetNodeExpression(newContext);
            }
            return base.VisitMethodCall(node);
        }
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (expressionsToReplace.ContainsKey(node))
            {
                if (replaceInline || replaceWithNewContext)
                    return newContext;
                return expressionsToReplace[node].GetNodeExpression(newContext);
            }
            return base.VisitBinary(node);
        }
    }
}