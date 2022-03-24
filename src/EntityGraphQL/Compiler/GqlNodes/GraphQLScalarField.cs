using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLScalarField : BaseGraphQLField
    {
        private readonly ExpressionExtractor extractor;
        private readonly ParameterReplacer replacer;
        private List<GraphQLScalarField>? extractedFields;

        public GraphQLScalarField(IEnumerable<IFieldExtension>? fieldExtensions, string name, Expression nextFieldContext, ParameterExpression? rootParameter, IGraphQLNode parentNode)
            : base(name, nextFieldContext, rootParameter, parentNode)
        {
            this.fieldExtensions = fieldExtensions?.ToList() ?? new List<IFieldExtension>();
            Name = name;
            extractor = new ExpressionExtractor();
            replacer = new ParameterReplacer();
        }

        public override bool HasAnyServices(IEnumerable<GraphQLFragmentStatement> fragments)
        {
            return Services.Any();
        }

        public override IEnumerable<BaseGraphQLField> Expand(List<GraphQLFragmentStatement> fragments, bool withoutServiceFields)
        {
            if (withoutServiceFields && Services.Any())
            {
                var extractedFields = ExtractFields();
                if (extractedFields != null)
                    return extractedFields;
            }
            return new List<BaseGraphQLField> { this };
        }

        private IEnumerable<BaseGraphQLField>? ExtractFields()
        {
            if (extractedFields != null)
                return extractedFields;

            extractedFields = extractor.Extract(NextFieldContext!, RootParameter!)?.Select(i => new GraphQLScalarField(null, i.Key, i.Value, RootParameter!, ParentNode!)).ToList();
            return extractedFields;
        }

        public override Expression? GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext = null, bool isRoot = false, bool contextChanged = false)
        {
            if (withoutServiceFields && Services.Any())
                return null;

            var newExpression = NextFieldContext!;
            if (contextChanged && Name != "__typename")
            {
                var selectedField = replacementNextFieldContext?.Type.GetField(Name);
                if (!Services.Any() && selectedField != null)
                    newExpression = Expression.Field(replacementNextFieldContext, Name);
                else
                    newExpression = replacer.ReplaceByType(newExpression, ParentNode!.NextFieldContext!.Type, replacementNextFieldContext!);

            }
            newExpression = ProcessScalarExpression(newExpression, replacer);
            return newExpression;
        }
    }
}