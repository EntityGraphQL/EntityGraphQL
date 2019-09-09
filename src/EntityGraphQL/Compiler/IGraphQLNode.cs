using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// GraphQLVisitor returns IGraphQLNodes as it processors the tree
    /// </summary>
    public interface IGraphQLNode : IGraphQLBaseNode
    {
        /// <summary>
        /// Execute this node with the supplied arguments. This is used for top level fields (that are each a query/mutation themselves)
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        object Execute(params object[] args);
        /// <summary>
        /// List of fields in the current node. E.g.
        /// {
        ///     movies { id name }
        /// }
        /// If this node if movies the list of fields is [id, name]. If this node is the root the fields is [movies]
        /// </summary>
        /// <value></value>
        IEnumerable<IGraphQLNode> Fields { get; }
        /// <summary>
        /// Any parameters used in the expression. These are parameters that need to be passed into the execution of the final query
        /// </summary>
        /// <value></value>
        List<ParameterExpression> Parameters { get; }
        /// <summary>
        /// Any parameters and values for constant values.
        /// </summary>
        /// <value></value>
        IReadOnlyDictionary<ParameterExpression, object> ConstantParameters { get; }

        /// <summary>
        /// Get the expression that would create this node. E.g. it may be db => db.Movies.Where(...)
        /// </summary>
        /// <value></value>
        ExpressionResult GetNodeExpression();

        /// <summary>
        /// Set the expression that would create this node. E.g. it may be db => db.Movies.Where(...)
        /// </summary>
        /// <param name="expr"></param>
        void SetNodeExpression(ExpressionResult expr);
    }

    public interface IGraphQLBaseNode
    {
        /// <summary>
        /// Name of the node
        /// </summary>
        /// <value></value>
        string Name { get; }
        OperationType Type { get; }
    }

    public enum OperationType
    {
        Query,
        Mutation,
        Fragment,
        Result,
    }
}
