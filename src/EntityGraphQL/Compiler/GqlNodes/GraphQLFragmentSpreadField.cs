using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Directives;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLFragmentSpreadField : BaseGraphQLField
    {
        public GraphQLFragmentSpreadField(ISchemaProvider schema, string name, Expression? nodeExpression, ParameterExpression rootParameter, IGraphQLNode parentNode)
            : base(schema, null, name, nodeExpression, rootParameter, parentNode, null)
        {
            LocationForDirectives = ExecutableDirectiveLocation.FRAGMENT_SPREAD;
        }

        public override bool HasAnyServices(IEnumerable<GraphQLFragmentStatement> fragments)
        {
            var graphQlFragmentStatements = fragments as GraphQLFragmentStatement[] ?? fragments.ToArray();

            return graphQlFragmentStatements.FirstOrDefault(f => f.Name == Name)!.QueryFields.Any(f => f.HasAnyServices(graphQlFragmentStatements));
        }

        protected override IEnumerable<BaseGraphQLField> ExpandField(CompileContext compileContext, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, Expression fieldContext, ParameterExpression? docParam, object? docVariables)
        {
            var fragment = fragments.FirstOrDefault(f => f.Name == Name) ?? throw new EntityGraphQLCompilerException($"Fragment {Name} not found in query document");
            var fields = fragment.QueryFields.SelectMany(f => f.Expand(compileContext, fragments, withoutServiceFields, fieldContext, docParam, docVariables));
            // the current op did not know about services in the fragment as the fragment definition may be after the operation in the query
            // we now know  if there are services we need to know about for executing
            var baseGraphQlFields = (fields as BaseGraphQLField[] ?? fields.ToArray()).ToList();
            if (!withoutServiceFields)
            {
                foreach (var field in baseGraphQlFields)
                {
                    GetServices(compileContext, field);
                }
            }
            baseGraphQlFields.AddRange(ExpandFromServices(withoutServiceFields, null));
            return baseGraphQlFields;
        }

        internal override IEnumerable<BaseGraphQLField> ExpandFromServices(bool withoutServiceFields, BaseGraphQLField? field)
        {
            if (withoutServiceFields && Field?.ExtractedFieldsFromServices != null)
                return Field.ExtractedFieldsFromServices.ToList();

            // we do not want to return the fragment field
            return withoutServiceFields && HasServices ? new List<BaseGraphQLField>() : (field != null ? new List<BaseGraphQLField> { field } : new List<BaseGraphQLField>());
        }

        private void GetServices(CompileContext compileContext, BaseGraphQLField gqlField)
        {
            if (gqlField.Field != null && gqlField.Field.Services.Any())
            {
                compileContext.AddServices(gqlField.Field.Services);
            }
            foreach (var subField in gqlField.QueryFields)
            {
                GetServices(compileContext, subField);
            }
        }

        protected override Expression? GetFieldExpression(CompileContext compileContext, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext, bool isRoot, bool contextChanged, ParameterReplacer replacer)
        {
            throw new EntityGraphQLCompilerException($"Fragment should have expanded out into non fragment fields");
        }
    }
}