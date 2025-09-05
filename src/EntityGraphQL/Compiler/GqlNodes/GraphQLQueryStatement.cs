using System;
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

    protected override ExecutableDirectiveLocation ExecutableDirectiveLocation => ExecutableDirectiveLocation.QUERY;
    protected override ISchemaType SchemaType => Schema.GetSchemaType(Schema.QueryContextType, false, null)!;

    protected override async Task<(object? data, bool didExecute, List<GraphQLError> errors)> ExecuteOperationField<TContext>(
        CompileContext compileContext,
        BaseGraphQLField field,
        TContext context,
        IServiceProvider? serviceProvider,
        IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments,
        IArgumentsTracker? docVariables
    )
    {
        // apply directives
        foreach (var directive in field.Directives)
        {
            if (directive.VisitNode(ExecutableDirectiveLocation.FIELD, Schema, field, Arguments, null, null) == null)
                return (null, false, []);
        }
        (var data, var didExecute) = await CompileAndExecuteNodeAsync(compileContext, context!, serviceProvider, fragments, field, docVariables);

        return (data, didExecute, new List<GraphQLError>());
    }
}
