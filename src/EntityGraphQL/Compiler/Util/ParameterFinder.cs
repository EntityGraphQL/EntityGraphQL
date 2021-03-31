using System.Linq.Expressions;

namespace EntityGraphQL.Compiler.Util
{
    public class ParameterFinder : ExpressionVisitor
    {
        private ParameterExpression find;
        private bool found = false;
        public bool Find(Expression node, ParameterExpression find)
        {
            this.find = find;
            found = false;
            Visit(node);
            return found;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (find == node)
                found = true;
            return base.VisitParameter(node);
        }
    }
}