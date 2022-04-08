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
        public BaseGraphQLQueryField? ResultSelection { get; set; }

        public GraphQLMutationField(string name, MutationField mutationField, Dictionary<string, Expression>? args, Expression nextFieldContext, ParameterExpression rootParameter, IGraphQLNode parentNode)
            : base(name, nextFieldContext, rootParameter, parentNode, args)
        {
            this.mutationField = mutationField;
        }

        public async Task<object?> ExecuteMutationAsync<TContext>(TContext context, GraphQLValidator validator, IServiceProvider serviceProvider, Func<string, string> fieldNamer)
        {
            try
            {
                return await mutationField.CallAsync(context, arguments, validator, serviceProvider, fieldNamer);
            }
            catch (EntityQuerySchemaException e)
            {
                throw new EntityQuerySchemaException($"Error applying mutation: {e.Message}", e);
            }
        }

        public override Expression? GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, Dictionary<string, Expression> parentArguments, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext = null, bool isRoot = false, bool contextChanged = false)
        {
            if (ResultSelection == null)
                throw new EntityGraphQLCompilerException($"Mutation {Name} should have a result selection");

            return ResultSelection.GetNodeExpression(serviceProvider, fragments, parentArguments, schemaContext, withoutServiceFields, replacementNextFieldContext);
        }
    }
}
