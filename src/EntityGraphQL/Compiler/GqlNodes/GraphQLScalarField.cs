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
        public GraphQLScalarField(ISchemaProvider schema, IField field, string name, Expression nextFieldContext, ParameterExpression? rootParameter, IGraphQLNode parentNode, Dictionary<string, object>? arguments)
            : base(schema, field, name, nextFieldContext, rootParameter, parentNode, arguments)
        {
        }

        public override bool HasAnyServices(IEnumerable<GraphQLFragmentStatement> fragments)
        {
            return Field?.Services.Any() == true;
        }

        public override IEnumerable<BaseGraphQLField> Expand(CompileContext compileContext, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, Expression fieldContext, ParameterExpression? docParam, object? docVariables)
        {
            var result = ProcessFieldDirectives(this, docParam, docVariables);
            if (result == null)
                return new List<BaseGraphQLField>();

            return base.ExpandFromServices(withoutServiceFields, result);
        }

        public override Expression? GetNodeExpression(CompileContext compileContext, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext, bool isRoot, bool contextChanged, ParameterReplacer replacer)
        {
            if (withoutServiceFields && Field?.Services.Any() == true)
                return null;

            (var result, var argumentValues) = Field!.GetExpression(NextFieldContext!, replacementNextFieldContext, ParentNode!, schemaContext, ResolveArguments(Arguments), docParam, docVariables, directives, contextChanged, replacer);

            if (argumentValues != null)
                compileContext.AddConstant(Field!.ArgumentParam!, argumentValues);
            if (result == null)
                return null;

            var newExpression = result;

            if (contextChanged && replacementNextFieldContext != null)
            {
                newExpression = ReplaceContext(replacementNextFieldContext!, isRoot, replacer, newExpression!);
            }
            newExpression = ProcessScalarExpression(newExpression, replacer);

            if (Field?.Services.Any() == true)
                compileContext.AddServices(Field.Services);
            return newExpression;
        }
    }
}