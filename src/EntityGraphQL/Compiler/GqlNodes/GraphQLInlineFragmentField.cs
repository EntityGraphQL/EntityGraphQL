using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLInlineFragmentField : BaseGraphQLField
    {
        public GraphQLInlineFragmentField(ISchemaProvider schema, string name, Expression? nodeExpression, ParameterExpression rootParameter, IGraphQLNode parentNode)
            : base(schema, null, name, nodeExpression, rootParameter, parentNode, null)
        {
        }

        public override IEnumerable<BaseGraphQLField> Expand(CompileContext compileContext, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, Expression fieldContext, ParameterExpression? docParam, object? docVariables)
        {
            return this.QueryFields;
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

        public override Expression? GetNodeExpression(CompileContext compileContext, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext, bool isRoot, bool contextChanged, ParameterReplacer replacer)
        {
            throw new EntityGraphQLCompilerException($"Fragment should have expanded out into non fragment fields");
        }
    }
}