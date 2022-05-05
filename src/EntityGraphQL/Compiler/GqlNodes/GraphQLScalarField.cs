using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLScalarField : BaseGraphQLField
    {
        public GraphQLScalarField(ISchemaProvider schema, IField? field, string name, Expression nextFieldContext, ParameterExpression? rootParameter, IGraphQLNode parentNode, Dictionary<string, object>? arguments)
            : base(schema, field, name, nextFieldContext, rootParameter, parentNode, arguments)
        {
            Name = name;
        }

        public override bool HasAnyServices(IEnumerable<GraphQLFragmentStatement> fragments)
        {
            return Field?.Services.Any() == true;
        }

        public override IEnumerable<BaseGraphQLField> Expand(List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, Expression fieldContext, ParameterExpression? docParam, object? docVariables)
        {
            var result = ProcessFieldDirectives(this, docParam, docVariables);
            if (result == null)
                return new List<BaseGraphQLField>();

            if (withoutServiceFields && Field?.Services.Any() == true)
            {
                var extractedFields = ExtractFields(fieldContext);
                if (extractedFields != null)
                    return extractedFields;
            }
            return new List<BaseGraphQLField> { result };
        }

        private IEnumerable<BaseGraphQLField>? ExtractFields(Expression fieldContext)
        {
            var extractor = new ExpressionExtractor();
            var extractedFields = extractor.Extract(NextFieldContext!, fieldContext, true)?.Select(i => new GraphQLExtractedField(schema, i.Key, i.Value, fieldContext)).ToList();
            return extractedFields;
        }

        public override Expression? GetNodeExpression(IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext, bool isRoot, bool contextChanged, ParameterReplacer replacer)
        {
            if (withoutServiceFields && Field?.Services.Any() == true)
                return null;

            (var result, var argumentValues) = Field!.GetExpression(NextFieldContext!, replacementNextFieldContext, ParentNode!, schemaContext, ResolveArguments(Arguments), docParam, docVariables, directives, contextChanged, replacer);

            if (argumentValues != null)
                constantParameters[Field!.ArgumentParam!] = argumentValues;
            if (result == null)
                return null;

            var newExpression = result;

            if (contextChanged && replacementNextFieldContext != null)
            {
                var selectedField = replacementNextFieldContext.Type.GetField(Name);
                if (!Field?.Services.Any() == true && selectedField != null)
                    newExpression = Expression.Field(replacementNextFieldContext, Name);
                else
                    newExpression = replacer.ReplaceByType(newExpression, ParentNode!.NextFieldContext!.Type, replacementNextFieldContext!);

            }
            newExpression = ProcessScalarExpression(newExpression, replacer);
            return newExpression;
        }
    }
}