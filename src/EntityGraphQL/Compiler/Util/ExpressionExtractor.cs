using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Compiler.Util
{
    /// <summary>
    /// Extracts expression with the root context as the provided ParameterExpression.
    /// Useful for getting required fields out of a WithService() call
    /// </summary>
    internal class ExpressionExtractor : ExpressionVisitor
    {
        private Expression? rootContext;
        private Dictionary<string, Expression>? extractedExpressions;
        private Expression? currentExpression;
        private string? contextParamFieldName;
        private bool matchByType;
        private string? rootFieldName;

        internal IDictionary<string, Expression>? Extract(Expression node, Expression rootContext, bool matchByType = false, string? rootFieldName = null)
        {
            this.rootContext = rootContext;
            extractedExpressions = new Dictionary<string, Expression>();
            currentExpression = null;
            contextParamFieldName = null;
            this.matchByType = matchByType;
            this.rootFieldName = rootFieldName;
            Visit(node);
            return extractedExpressions.Count > 0 ? extractedExpressions : null;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (rootContext == null)
                throw new EntityGraphQLCompilerException("Root context not set for ExpressionExtractor");

            if (rootContext == currentExpression)
                throw new EntityGraphQLCompilerException($"The context parameter {node.Name} used in a WithService() field is not allowed. Please select the specific fields required from the context parameter.");
            if ((rootContext == node || (matchByType && rootContext.Type == node.Type)) && currentExpression != null && contextParamFieldName != null)
                extractedExpressions![contextParamFieldName] = currentExpression;
            return base.VisitParameter(node);
        }
        protected override Expression VisitBinary(BinaryExpression node)
        {
            // reset currents
            currentExpression = null;
            contextParamFieldName = null;
            var left = base.Visit(node.Left);

            currentExpression = null;
            contextParamFieldName = null;
            var right = base.Visit(node.Right);
            return Expression.MakeBinary(node.NodeType, left, right);
        }
        protected override Expression VisitMember(MemberExpression node)
        {
            var weCaptured = false;
            // if is is a nullable type we want to extract the nullable field not the nullableField.HasValue/Value
            // node.Expression can be null if it is a static member - e.g. DateTime.MaxValue
            if (currentExpression == null && !node.Expression?.Type.IsNullableType() == true)
            {
                currentExpression = node;
                contextParamFieldName = node.Member.Name;
                weCaptured = true;
            }
            var result = base.VisitMember(node);
            if (weCaptured)
            {
                currentExpression = null;
                contextParamFieldName = null;
            }
            return result;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Object != null)
            {
                var prevExp = currentExpression;
                currentExpression = null;
                Visit(node.Object);
                currentExpression = prevExp;
            }
            var startAt = 0;
            if (node.Object is null) // static method
            {
                startAt = 1;
                currentExpression = node;
                contextParamFieldName = rootFieldName;
                Visit(node.Arguments[0]);
            }
            for (int i = startAt; i < node.Arguments.Count; i++)
            {
                Expression arg = node.Arguments[i];
                currentExpression = null;
                contextParamFieldName = null;
                Visit(arg);
            }
            currentExpression = null;
            contextParamFieldName = null;
            return node;
        }
    }
}