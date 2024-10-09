using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Directives;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

public class GraphQLQueryStatement : ExecutableGraphQLStatement
{
    public GraphQLQueryStatement(ISchemaProvider schema, string? name, Expression nodeExpression, ParameterExpression rootParameter, Dictionary<string, ArgType> variables)
        : base(schema, name, nodeExpression, rootParameter, variables) { }

    public override Task<ConcurrentDictionary<string, object?>> ExecuteAsync<TContext>(
        TContext? context,
        IServiceProvider? serviceProvider,
        List<GraphQLFragmentStatement> fragments,
        Func<string, string> fieldNamer,
        ExecutionOptions options,
        QueryVariables? variables,
        QueryRequestContext requestContext
    )
        where TContext : default
    {
        Schema.CheckTypeAccess(Schema.GetSchemaType(Schema.QueryContextType, false, null), requestContext);

        var result = new ConcurrentDictionary<string, object?>();
        // pass to directives
        foreach (var directive in Directives)
        {
            if (directive.VisitNode(ExecutableDirectiveLocation.QUERY, Schema, this, Arguments, null, null) == null)
                return Task.FromResult(result);
        }
        return base.ExecuteAsync(context, serviceProvider, fragments, fieldNamer, options, variables, requestContext);
    }
}
