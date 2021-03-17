using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLScalarField : BaseGraphQLField
    {
        private readonly ExpressionResult expression;
        private bool hasAnyServices;

        public GraphQLScalarField(string name, ExpressionResult expression, ParameterExpression contextParameter)
        {
            Name = name;
            this.expression = expression;
            RootFieldParameter = contextParameter;
            constantParameters = expression.ConstantParameters.ToDictionary(i => i.Key, i => i.Value);
        }

        public override bool HasAnyServices { get => Services.Any() || hasAnyServices; set => hasAnyServices = value; }

        public override ExpressionResult GetNodeExpression(object contextValue, IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields = false, ParameterExpression buildServiceWrapWithParam = null)
        {
            return expression;
        }
    }
}