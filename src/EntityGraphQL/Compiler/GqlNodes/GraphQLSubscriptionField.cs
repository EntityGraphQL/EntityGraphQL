using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLSubscriptionField : BaseGraphQLQueryField
    {
        public SubscriptionField SubscriptionField { get; set; }

        public BaseGraphQLQueryField? ResultSelection { get; set; }

        public GraphQLSubscriptionField(ISchemaProvider schema, string name, SubscriptionField subscriptionField, Dictionary<string, object>? args, Expression nextFieldContext, ParameterExpression rootParameter, IGraphQLNode parentNode)
            : base(schema, subscriptionField, name, nextFieldContext, rootParameter, parentNode, args)
        {
            this.SubscriptionField = subscriptionField;
        }

        public async Task<object?> ExecuteSubscriptionAsync<TContext>(TContext context, GraphQLValidator validator, IServiceProvider? serviceProvider, ParameterExpression? variableParameter, object? variablesToUse)
        {
            try
            {
                return await SubscriptionField.CallAsync(context, Arguments, validator, serviceProvider, variableParameter, variablesToUse);
            }
            catch (EntityQuerySchemaException e)
            {
                throw new EntityQuerySchemaException($"Error registering subscription: {e.Message}", e);
            }
        }

        public override Expression? GetNodeExpression(CompileContext compileContext, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext, bool isRoot, bool contextChanged, ParameterReplacer replacer)
        {
            if (ResultSelection == null)
                throw new EntityGraphQLCompilerException($"Subscription {Name} should have a result selection");

            return ResultSelection.GetNodeExpression(compileContext, serviceProvider, fragments, docParam, docVariables, schemaContext, withoutServiceFields, replacementNextFieldContext, isRoot, contextChanged, replacer);
        }
    }
}
