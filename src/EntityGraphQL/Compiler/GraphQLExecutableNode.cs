using System;
using System.Threading.Tasks;

namespace EntityGraphQL.Compiler
{
    public abstract class GraphQLExecutableNode
    {
        public abstract Task<object> ExecuteAsync<TContext>(TContext context, GraphQLValidator validator, IServiceProvider serviceProvider);
    }
}