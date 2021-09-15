using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLMutationField : BaseGraphQLQueryField
    {
        private readonly MutationType mutationType = null;
        private readonly Dictionary<string, Expression> args = null;
        public BaseGraphQLQueryField ResultSelection { get; set; }

        public GraphQLMutationField(string name, MutationType mutationType, Dictionary<string, Expression> args, Expression nextFieldContext, ParameterExpression rootParameter, IGraphQLNode parentNode)
            : base(name, nextFieldContext, rootParameter, parentNode)
        {
            this.mutationType = mutationType;
            this.args = args;
        }

        public async Task<object> ExecuteMutationAsync<TContext>(TContext context, GraphQLValidator validator, IServiceProvider serviceProvider, Func<string, string> fieldNamer)
        {
            try
            {
                return await mutationType.CallAsync(context, args, validator, serviceProvider, fieldNamer);
            }
            catch (EntityQuerySchemaException e)
            {
                throw new EntityQuerySchemaException($"Error applying mutation: {e.Message}", e);
            }
        }

        public override Expression GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression schemaContext, bool withoutServiceFields, Expression replacementNextFieldContext = null, bool isRoot = false, bool contextChanged = false)
        {
            return ResultSelection.GetNodeExpression(serviceProvider, fragments, schemaContext, withoutServiceFields, replacementNextFieldContext);
        }
    }
}
