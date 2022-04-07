using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Compiler
{
    public class GraphQLScalarField : BaseGraphQLField
    {
        private readonly ParameterReplacer replacer;
        private List<GraphQLScalarField> extractedFields;
        private readonly Field field;

        public GraphQLScalarField(Field field, IEnumerable<IFieldExtension> fieldExtensions, string name, Expression nextFieldContext, ParameterExpression rootParameter, IGraphQLNode parentNode, Dictionary<string, Expression> arguments)
            : base(name, nextFieldContext, rootParameter, parentNode, arguments)
        {
            this.fieldExtensions = fieldExtensions?.ToList();
            Name = name;
            replacer = new ParameterReplacer();
            this.field = field;
            this.AddServices(field.Services);
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

        private IEnumerable<BaseGraphQLField> ExtractFields()
        {
            if (extractedFields != null)
                return extractedFields;

            var extractor = new ExpressionExtractor();
            extractedFields = extractor.Extract(NextFieldContext, ParentNode.NextFieldContext, true)?.Select(i => new GraphQLScalarField(field, null, i.Key, i.Value, RootParameter, ParentNode, arguments)
            {
                // do not carry the services over
                Services = new List<Type>()
            }).ToList();
            return extractedFields;
        }

        public override Expression GetNodeExpression(IServiceProvider serviceProvider, List<GraphQLFragmentStatement> fragments, Dictionary<string, Expression> parentArguments, ParameterExpression schemaContext, bool withoutServiceFields, Expression replacementNextFieldContext = null, bool isRoot = false, bool contextChanged = false)
        {
            if (withoutServiceFields && Services.Any())
                return null;

            var result = field.GetExpression(NextFieldContext, replacementNextFieldContext ?? ParentNode.NextFieldContext, schemaContext, parentArguments.MergeNew(arguments), contextChanged);
            AddConstantParameters(result.ConstantParameters);
            AddServices(result.Services);

            var newExpression = result.Expression;

            if (contextChanged && Name != "__typename" && replacementNextFieldContext != null)
            {
                var selectedField = replacementNextFieldContext.Type.GetField(Name);
                if (!Services.Any() && selectedField != null)
                    newExpression = Expression.Field(replacementNextFieldContext, Name);
                else
                    newExpression = replacer.ReplaceByType(newExpression, ParentNode.NextFieldContext.Type, replacementNextFieldContext);

            }
            newExpression = ProcessScalarExpression(newExpression, replacer);
            return newExpression;
        }
    }
}