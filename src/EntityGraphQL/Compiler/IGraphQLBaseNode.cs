using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    public interface IGraphQLBaseNode
    {
        /// <summary>
        /// Name of the node
        /// </summary>
        /// <value></value>
        string Name { get; set; }
        IReadOnlyDictionary<ParameterExpression, object> ConstantParameters { get; }
        ParameterExpression FieldParameter { get; }
        IEnumerable<Type> Services { get; }

        /// <summary>
        /// Get the expression that would create this node. E.g. it may be db => db.Movies.Where(...) or a field selection movie => movie.Name
        /// </summary>
        /// <value></value>
        ExpressionResult GetNodeExpression(object contextValue, IServiceProvider serviceProvider);
        /// <summary>
        /// Update the expression that creates this node
        /// </summary>
        /// <param name="expressionResult"></param>
        void SetNodeExpression(ExpressionResult expressionResult);
        void SetCombineExpression(Expression item2);
    }

    public enum OperationType
    {
        Query,
        Mutation,
        Fragment,
        Result,
    }
}
