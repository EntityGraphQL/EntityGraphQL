using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EntityGraphQL.Compiler.Util
{
    /// <summary>
    /// As people build schema fields they may be against different parameters, this visitor lets us change it to the one used in compiling the EQL.
    /// I.e. in (p_0) => p_0.blah `p_0` needs to be the same object otherwise it expects 2 values passed in.
    /// </summary>
    public class ParameterReplacer : ExpressionVisitor
    {
        private Expression newParam;
        private Type toReplaceType;
        private ParameterExpression toReplace;
        private string newFieldName;
        private bool finished;

        /// <summary>
        /// Rebuilds the expression by replacing toReplace with newParam. Optionally looks for newFieldName as it is rebuilding.
        /// Used when rebuilding expressions to happen from the sans-services results
        /// </summary>
        /// <param name="node"></param>
        /// <param name="toReplace"></param>
        /// <param name="newParam"></param>
        /// <param name="newFieldName"></param>
        /// <returns></returns>
        public Expression Replace(Expression node, ParameterExpression toReplace, Expression newParam, string newFieldName = null)
        {
            this.newParam = newParam;
            this.toReplace = toReplace;
            this.toReplaceType = null;
            this.newFieldName = newFieldName;
            finished = false;
            return Visit(node);
        }

        public Expression ReplaceByType(Expression node, Type toReplaceType, Expression newParam)
        {
            this.newParam = newParam;
            this.toReplaceType = toReplaceType;
            this.toReplace = null;
            this.newFieldName = null;
            finished = false;
            return Visit(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (toReplace != null && toReplace == node)
                return newParam;
            if (toReplaceType != null && node.NodeType == ExpressionType.Parameter && toReplaceType == node.Type)
                return newParam;
            return node;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            var p = node.Parameters.Select(base.Visit).Cast<ParameterExpression>();
            var body = base.Visit(node.Body);
            return Expression.Lambda(body, p);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // returned expression may have been modified and we need to rebuild
            if (node.Expression != null)
            {
                var nodeExp = base.Visit(node.Expression);
                if (finished)
                    return nodeExp;

                var nextField = (MemberInfo)nodeExp.Type.GetProperties().Where(p => p.Name.ToLower() == node.Member.Name.ToLower()).FirstOrDefault() ??
                    nodeExp.Type.GetFields().Where(f => f.Name.ToLower() == node.Member.Name.ToLower()).FirstOrDefault();
                if (newFieldName != null && nextField == null)
                {
                    finished = true;
                    nodeExp = Expression.PropertyOrField(nodeExp, newFieldName);
                }
                else
                    nodeExp = Expression.PropertyOrField(nodeExp, node.Member.Name);
                return nodeExp;
            }
            return base.VisitMember(node);
        }
    }
}