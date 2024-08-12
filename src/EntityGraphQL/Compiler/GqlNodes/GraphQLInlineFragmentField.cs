using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Directives;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLInlineFragmentField : BaseGraphQLField
    {
        public GraphQLInlineFragmentField(ISchemaProvider schema, string name, Expression? nodeExpression, ParameterExpression rootParameter, IGraphQLNode parentNode)
            : base(schema, null, name, nodeExpression, rootParameter, parentNode, null)
        {
            LocationForDirectives = ExecutableDirectiveLocation.INLINE_FRAGMENT;
        }

        public override bool HasServicesAtOrBelow(IEnumerable<GraphQLFragmentStatement> fragments)
        {
            return QueryFields.Any(x => x.HasServices);
        }

        protected override IEnumerable<BaseGraphQLField> ExpandField(
            CompileContext compileContext,
            List<GraphQLFragmentStatement> fragments,
            bool withoutServiceFields,
            Expression fieldContext,
            ParameterExpression? docParam,
            object? docVariables
        )
        {
            return QueryFields.SelectMany(x => x.Expand(compileContext, fragments, withoutServiceFields, fieldContext, docParam, docVariables));
        }

        internal override IEnumerable<BaseGraphQLField> ExpandFromServices(bool withoutServiceFields, BaseGraphQLField? field)
        {
            if (withoutServiceFields && Field?.ExtractedFieldsFromServices != null)
                return Field.ExtractedFieldsFromServices.ToList();

            // we do not want to return the fragment field
            return withoutServiceFields && HasServices ? new List<BaseGraphQLField>() : (field != null ? new List<BaseGraphQLField> { field } : new List<BaseGraphQLField>());
        }

        private static void GetServices(CompileContext compileContext, BaseGraphQLField gqlField)
        {
            if (gqlField.Field != null && gqlField.Field.Services.Count > 0)
            {
                compileContext.AddServices(gqlField.Field.Services);
            }
            foreach (var subField in gqlField.QueryFields)
            {
                GetServices(compileContext, subField);
            }
        }

        protected override Expression? GetFieldExpression(
            CompileContext compileContext,
            IServiceProvider? serviceProvider,
            List<GraphQLFragmentStatement> fragments,
            ParameterExpression? docParam,
            object? docVariables,
            ParameterExpression schemaContext,
            bool withoutServiceFields,
            Expression? replacementNextFieldContext,
            List<Type>? possibleNextContextTypes,
            bool contextChanged,
            ParameterReplacer replacer
        )
        {
            throw new EntityGraphQLCompilerException($"Fragment should have expanded out into non fragment fields");
        }
    }
}
