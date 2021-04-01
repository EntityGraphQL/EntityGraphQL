using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Compiler
{
    public class GraphQLScalarField : BaseGraphQLField
    {
        public override bool IsScalar { get => true; }

        private readonly ExpressionResult expression;
        private bool hasAnyServices;

        public GraphQLScalarField(string name, ExpressionResult expression, ParameterExpression contextParameter)
        {
            Name = name;
            this.expression = expression;
            RootFieldParameter = contextParameter;
            constantParameters = expression.ConstantParameters.ToDictionary(i => i.Key, i => i.Value);
            AddServices(expression.Services);
        }

        public override bool HasAnyServices { get => Services.Any() || hasAnyServices; set => hasAnyServices = value; }

        public override ExpressionResult GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields = false, ParameterExpression replaceContextWith = null, bool isRoot = false)
        {
            if (withoutServiceFields && HasAnyServices)
                return null;

            if (replaceContextWith != null)
            {
                var replacer = new ParameterReplacer();
                var newExpression = (ExpressionResult)replacer.Replace(expression, RootFieldParameter, replaceContextWith);
                newExpression.AddServices(expression.Services);
                return newExpression;
            }
            return expression;
        }
    }
}