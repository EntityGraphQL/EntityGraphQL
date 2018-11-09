using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Holds information about the compiled expression.
    /// The Expression and any ParameterExpressions and their values.
    /// We use ParameterExpression and not Constant so the compiled query can be cached and values swapped out
    /// </summary>
    public class ExpressionResult
    {
        private Dictionary<ParameterExpression, object> constantParameters = new Dictionary<ParameterExpression, object>();

        public ExpressionResult(Expression value)
        {
            this.Expression = value;
        }

        public virtual Expression Expression { get; internal set; }
        public Type Type { get { return Expression.Type; } }

        public IReadOnlyDictionary<ParameterExpression, object> ConstantParameters { get => constantParameters; }
        public ExpressionType NodeType { get { return Expression.NodeType; } }

        public static implicit operator Expression(ExpressionResult field)
        {
            return field.Expression;
        }

        /// <summary>
        /// Explicitly cast an Expression to ExpressionResult creating a new ExpressionResult. Make sure that is your intention, not carrying over any parameters
        /// </summary>
        /// <param name="value"></param>
        public static explicit operator ExpressionResult(Expression value)
        {
            return new ExpressionResult(value);
        }

        internal void AddConstantParameter(ParameterExpression type, object value)
        {
            constantParameters.Add(type, value);
        }
    }
}