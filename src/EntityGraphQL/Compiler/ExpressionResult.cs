using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Holds information about the compiled expression.
    /// The Expression and any ParameterExpressions and their values.
    /// We use ParameterExpression and not inline ConstantExpressions so the compiled query can be cached and values swapped out
    /// </summary>
    public class ExpressionResult
    {
        private readonly Dictionary<ParameterExpression, object> constantParameters = new Dictionary<ParameterExpression, object>();
        private readonly List<Type> services;

        public ExpressionResult(Expression value)
        {
            this.Expression = value;
            services = new List<Type>();
        }
        public ExpressionResult(Expression value, IEnumerable<Type> services) : this(value)
        {
            AddServices(services);
        }

        public virtual Expression Expression { get; internal set; }
        public Type Type { get { return Expression.Type; } }

        public IReadOnlyDictionary<ParameterExpression, object> ConstantParameters { get => constantParameters; }
        /// <summary>
        /// Services (DI) required by these expresion other than the schema context
        /// </summary>
        /// <value></value>
        public IEnumerable<Type> Services { get => services; }
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

        internal void AddConstantParameters(IReadOnlyDictionary<ParameterExpression, object> constantParameters)
        {
            foreach (var item in constantParameters)
            {
                AddConstantParameter(item.Key, item.Value);
            }
        }
        internal void AddServices(IEnumerable<Type> services)
        {
            if (services == null)
                return;

            this.services.AddRange(services);
        }
    }
}