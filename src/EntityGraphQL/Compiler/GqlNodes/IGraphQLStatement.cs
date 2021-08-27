namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// A top level statement in a GQL document. E.g. Query, Mutation, Fragment, Subscription
    /// TODO can remove?
    /// </summary>
    public interface IGraphQLStatement : IGraphQLNode
    {
    }
}