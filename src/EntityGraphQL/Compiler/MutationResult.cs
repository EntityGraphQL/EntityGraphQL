using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class MutationResult : ExpressionResult
    {
        private readonly Schema.MutationType mutationType;
        private readonly Expression paramExp;
        private readonly Dictionary<string, ExpressionResult> gqlRequestArgs;

        public MutationResult(Schema.MutationType mutationType, Dictionary<string, ExpressionResult> args) : base(null)
        {
            this.mutationType = mutationType;
            this.gqlRequestArgs = args;
            paramExp = Expression.Parameter(mutationType.ContextType);
        }

        public override Expression Expression { get { return paramExp; } }

        public object Execute(object[] externalArgs)
        {
            try
            {
                return mutationType.Call(externalArgs, gqlRequestArgs);
            }
            catch(EntityQuerySchemaException e)
            {
                throw new EntityQuerySchemaException($"Error applying mutation: {e.Message}");
            }
        }
    }
}