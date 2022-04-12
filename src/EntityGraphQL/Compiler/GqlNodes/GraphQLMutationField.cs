using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLMutationField : BaseGraphQLQueryField
    {
        public MutationField MutationField { get; set; }

        public BaseGraphQLQueryField? ResultSelection { get; set; }

        public GraphQLMutationField(string name, MutationField mutationField, Dictionary<string, object>? args, Expression nextFieldContext, ParameterExpression rootParameter, IGraphQLNode parentNode)
            : base(name, nextFieldContext, rootParameter, parentNode, args)
        {
            this.MutationField = mutationField;
        }

        public async Task<object?> ExecuteMutationAsync<TContext>(TContext context, GraphQLValidator validator, IServiceProvider serviceProvider, Func<string, string> fieldNamer, ParameterExpression? variableParameter, object? variablesToUse)
        {
            try
            {
                return await MutationField.CallAsync(context, arguments, validator, serviceProvider, variableParameter, variablesToUse, fieldNamer);
            }
            catch (EntityQuerySchemaException e)
            {
                throw new EntityQuerySchemaException($"Error applying mutation: {e.Message}", e);
            }
        }

        public override Expression? GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, Dictionary<string, object> parentArguments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext = null, bool isRoot = false, bool contextChanged = false)
        {
            if (ResultSelection == null)
                throw new EntityGraphQLCompilerException($"Mutation {Name} should have a result selection");

            return ResultSelection.GetNodeExpression(serviceProvider, fragments, parentArguments, docParam, docVariables, schemaContext, withoutServiceFields, replacementNextFieldContext);
        }
    }
}
