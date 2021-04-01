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
        private readonly ExpressionExtractor extractor;
        private readonly ParameterReplacer replacer;
        private List<GraphQLScalarField> extractedFields;

        public GraphQLScalarField(string name, ExpressionResult expression, ParameterExpression contextParameter)
        {
            Name = name;
            this.expression = expression;
            RootFieldParameter = contextParameter;
            constantParameters = expression.ConstantParameters.ToDictionary(i => i.Key, i => i.Value);
            AddServices(expression.Services);
            extractor = new ExpressionExtractor();
            replacer = new ParameterReplacer();
        }

        public override bool HasAnyServices { get => Services.Any(); }

        public override IEnumerable<BaseGraphQLField> Expand(List<GraphQLFragmentStatement> fragments, bool withoutServiceFields)
        {
            if (withoutServiceFields && Services.Any())
                return ExtractFields();
            return new List<BaseGraphQLField> { this };
        }

        private IEnumerable<BaseGraphQLField> ExtractFields()
        {
            if (extractedFields != null)
                return extractedFields;

            extractedFields = extractor.Extract(expression, RootFieldParameter).Select(i => new GraphQLScalarField(i.Key, (ExpressionResult)i.Value, RootFieldParameter)).ToList();
            return extractedFields;
        }

        public override ExpressionResult GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields = false, ParameterExpression replaceContextWith = null, bool isRoot = false)
        {
            if (withoutServiceFields && Services.Any())
                return null;

            if (replaceContextWith != null)
            {
                var newExpression = (ExpressionResult)replacer.Replace(expression, RootFieldParameter, replaceContextWith);
                newExpression.AddServices(expression.Services);
                return newExpression;
            }
            return expression;
        }
    }
}