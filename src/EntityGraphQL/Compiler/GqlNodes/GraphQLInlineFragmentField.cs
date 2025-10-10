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
        LocationForDirectives = ExecutableDirectiveLocation.InlineFragment;
    }

    public override bool HasServicesAtOrBelow(IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments)
    {
        return QueryFields.Any(x => x.HasServices);
    }

    protected override IEnumerable<BaseGraphQLField> ExpandField(
        CompileContext compileContext,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
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
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
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
        throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Inline fragment should have expanded out into non-fragment fields");
    }
}
