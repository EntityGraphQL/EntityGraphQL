using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Directives;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

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
        IArgumentsTracker? docVariables
    )
    {
        return QueryFields.SelectMany(x => x.Expand(compileContext, fragments, withoutServiceFields, fieldContext, docParam, docVariables));
    }

    protected override Expression? GetFieldExpression(
        CompileContext compileContext,
        IServiceProvider? serviceProvider,
        List<GraphQLFragmentStatement> fragments,
        ParameterExpression? docParam,
        IArgumentsTracker? docVariables,
        ParameterExpression schemaContext,
        bool withoutServiceFields,
        Expression? replacementNextFieldContext,
        List<Type>? possibleNextContextTypes,
        bool contextChanged,
        ParameterReplacer replacer
    )
    {
        throw new EntityGraphQLCompilerException($"Inline fragment should have expanded out into non-fragment fields");
    }
}
