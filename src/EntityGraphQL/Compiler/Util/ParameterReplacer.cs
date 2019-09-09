using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler.Util
{
	/// <summary>
	/// As people build schema fields they may be against different parameters, this visitor lets us change it to the one used in compiling the EQL.
	/// I.e. in (p_0) => p_0.blah `p_0` needs to be the same object otherwise it expects 2 values passed in.
	/// </summary>
	internal class ParameterReplacer : ExpressionVisitor
	{
		private Expression newParam;
		private Type toReplaceType;
		private ParameterExpression toReplace;
		internal Expression Replace(Expression node, ParameterExpression toReplace, Expression newParam)
		{
			this.newParam = newParam;
			this.toReplace = toReplace;
			return Visit(node);
		}

		internal Expression ReplaceByType(Expression node, Type toReplaceType, Expression newParam)
		{
			this.newParam = newParam;
			this.toReplaceType = toReplaceType;
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
			if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter && (node.Expression == toReplace || node.Expression.Type == toReplaceType))
			{
				// we may have replaced this parameter and the new type (anonymous) might have fields not properties
				var parm = base.Visit(node.Expression);
				var exp = Expression.PropertyOrField(parm, node.Member.Name);
				return exp;
			}
			return base.VisitMember(node);
		}
	}
}