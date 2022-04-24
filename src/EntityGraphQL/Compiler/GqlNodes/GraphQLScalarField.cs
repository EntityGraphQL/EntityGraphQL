using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLScalarField : BaseGraphQLField
    {
        private List<GraphQLScalarField>? extractedFields;

        public GraphQLScalarField(ISchemaProvider schema, IField? field, IEnumerable<IFieldExtension>? fieldExtensions, string name, Expression nextFieldContext, ParameterExpression? rootParameter, IGraphQLNode parentNode, Dictionary<string, object>? arguments)
            : base(schema, field, name, nextFieldContext, rootParameter, parentNode, arguments)
        {
            this.fieldExtensions = fieldExtensions?.ToList() ?? new List<IFieldExtension>();
            Name = name;
            this.AddServices(field?.Services);
        }

        public override bool HasAnyServices(IEnumerable<GraphQLFragmentStatement> fragments)
        {
            return Services.Any();
        }

        public override IEnumerable<BaseGraphQLField> Expand(List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, ParameterExpression? docParam, object? docVariables)
        {
            var result = ProcessFieldDirectives(this, docParam, docVariables);
            if (result == null)
                return new List<BaseGraphQLField>();

            if (withoutServiceFields && Services.Any())
            {
                var extractedFields = ExtractFields();
                if (extractedFields != null)
                    return extractedFields;
            }
            return new List<BaseGraphQLField> { result };
        }

        private IEnumerable<BaseGraphQLField>? ExtractFields()
        {
            if (extractedFields != null)
                return extractedFields;

            var extractor = new ExpressionExtractor();
            extractedFields = extractor.Extract(NextFieldContext!, ParentNode!.NextFieldContext!, true)?.Select(i => new GraphQLScalarField(schema, Field, null, i.Key, i.Value, RootParameter, ParentNode, Arguments)
            {
                // do not carry the services over
                Services = new List<Type>()
            }).ToList();
            return extractedFields;
        }

        public override Expression? GetNodeExpression(IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext, bool isRoot, bool contextChanged, ParameterReplacer replacer)
        {
            if (withoutServiceFields && Services.Any())
                return null;

            (var result, var argumentValues) = Field!.GetExpression(NextFieldContext!, replacementNextFieldContext, ParentNode!, schemaContext, Arguments, docParam, docVariables, directives, contextChanged, replacer);
            AddServices(Field!.Services);
            if (argumentValues != null)
                constantParameters[Field!.ArgumentParam!] = argumentValues;
            if (result == null)
                return null;

            var newExpression = result;

            if (contextChanged && Name != "__typename" && replacementNextFieldContext != null)
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