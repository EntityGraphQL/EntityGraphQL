using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler.Util
{
    /// <summary>
    /// Extracts expression with the root context as the provided ParameterExpression.
    /// Useful for getting required fields out of a WithService() call
    /// </summary>
    internal class ExpressionExtractor : ExpressionVisitor
    {
        private ParameterExpression rootContext;
        private Dictionary<string, Expression> extractedExpressions;
        private Expression currentExpression;
        private string contextParamFieldName;

        internal IDictionary<string, Expression> Extract(Expression node, ParameterExpression rootContext)
        {
            this.rootContext = rootContext;
            extractedExpressions = new Dictionary<string, Expression>();
            currentExpression = null;
            contextParamFieldName = null;
            Visit(node);
            return extractedExpressions;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (rootContext == currentExpression)
                throw new EntityGraphQLCompilerException($"The context parameter {node.Name} used in a WithService() field is not allowed. Please select the specific fields required from the context parameter.");
            if (rootContext == node && currentExpression != null)
                extractedExpressions.Add(contextParamFieldName, currentExpression);
            return base.VisitParameter(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (currentExpression != null)
                currentExpression = node;
            contextParamFieldName = node.Member.Name;
            return base.VisitMember(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (currentExpression != null)
                currentExpression = node;
            Visit(node.Object);
            foreach (var arg in node.Arguments)
            {
                currentExpression = arg;
                Visit(arg);
            }
            return node;
        }
    }
}