using System;
using System.Collections.Generic;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Directives
{
    public interface IDirectiveProcessor
    {
        string Name { get; }
        string Description { get; }
        List<ExecutableDirectiveLocation> Location { get; }
        /// <summary>
        /// Return the Type used for the directive arguments
        /// </summary>
        /// <returns></returns>
        Type GetArgumentsType();
        IDictionary<string, ArgType> GetArguments(ISchemaProvider schema);
        /// <summary>
        /// Called when the graphql node is first visited. Before any execution and expression building.
        /// You can use this is make a decision to include the field in the result or not
        /// </summary>
        /// <param name="location">The ExecutableDirectiveLocation where this directive is used</param>
        /// <param name="node">Information about the node the directive was set against. Will be null for VariableDefinition</param>
        /// <param name="arguments">Any arguments for the directive</param>
        /// <returns>Return node or a new modified IGraphQLNode. Return null to remove the node from execution</returns>
        IGraphQLNode? VisitNode(ExecutableDirectiveLocation location, IGraphQLNode? node, object? arguments);
    }
}