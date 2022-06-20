using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

internal class GraphQLExtractedField : BaseGraphQLField
{
    private readonly Expression fieldContext;

    public GraphQLExtractedField(ISchemaProvider schema, string name, Expression fieldExpression, Expression fieldContext)
    : base(schema, null, name, fieldExpression, null, null, null)
    {
        this.fieldContext = fieldContext;
    }

    public override IEnumerable<BaseGraphQLField> Expand(CompileContext compileContext, List<GraphQLFragmentStatement> fragments, bool withoutServiceFields, Expression fieldContext, ParameterExpression? docParam, object? docVariables)
    {
        throw new NotImplementedException();
    }

    public override Expression GetNodeExpression(CompileContext compileContext, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext, bool isRoot, bool contextChanged, ParameterReplacer replacer)
    {
        var exp = replacer.ReplaceByType(NextFieldContext!, fieldContext.Type, fieldContext);
        return exp;
    }
}