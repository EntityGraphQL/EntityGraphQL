using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLMutationField : BaseGraphQLQueryField
    {
        private readonly MutationField mutationField;
        private readonly Dictionary<string, Expression>? args;
        public BaseGraphQLQueryField? ResultSelection { get; set; }

        public GraphQLMutationField(string name, MutationField mutationType, Dictionary<string, Expression>? args, Expression nextFieldContext, ParameterExpression rootParameter, IGraphQLNode parentNode)
            : base(name, null, nextFieldContext, rootParameter, parentNode)
        {
            this.mutationField = mutationType;
            this.args = args;
        }

        public async Task<object?> ExecuteMutationAsync<TContext>(TContext context, GraphQLValidator validator, IServiceProvider serviceProvider, Func<string, string> fieldNamer)
        {
            try
            {
                return await mutationField.CallAsync(context, args, validator, serviceProvider, fieldNamer);
            }
            catch (EntityQuerySchemaException e)
            {
                throw new EntityQuerySchemaException($"Error applying mutation: {e.Message}", e);
            }
        }

        public override Expression? GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext = null, bool isRoot = false, bool contextChanged = false)
        {
            if (ResultSelection == null)
            {
                throw new EntityGraphQLCompilerException($"Mutation {Name} has no result selection");
            }
            return ResultSelection.GetNodeExpression(serviceProvider, fragments, schemaContext, withoutServiceFields, replacementNextFieldContext);
        }
    }
}
