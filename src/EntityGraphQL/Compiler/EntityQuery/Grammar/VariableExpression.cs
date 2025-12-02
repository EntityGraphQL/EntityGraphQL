using System;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.EntityQuery.Grammar;

/// <summary>
/// Expression that resolves a GraphQL variable ($variableName) during filter compilation
/// </summary>
public class VariableExpression : IExpression
{
    private readonly string variableName;
    private readonly EqlCompileContext compileContext;

    public VariableExpression(string variableName, EqlCompileContext compileContext)
    {
        this.variableName = variableName ?? throw new ArgumentNullException(nameof(variableName));
        this.compileContext = compileContext ?? throw new ArgumentNullException(nameof(compileContext));
    }

    public Type Type => typeof(object); // Will be resolved at compile time

    public Expression Compile(Expression? context, EntityQueryParser parser, ISchemaProvider? schema, QueryRequestContext requestContext, IMethodProvider methodProvider)
    {
        // Check if we have variable information in the compile context
        if (compileContext.DocumentVariables == null)
        {
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Variable ${variableName} not found in variables.");
        }

        if (compileContext.DocumentVariablesParameter == null)
        {
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Variable ${variableName} not found in variables.");
        }

        // Check if the variable exists in the actual variables and get its value
        var variableAccessExpression = Expression.PropertyOrField(compileContext.DocumentVariablesParameter, variableName);
        if (variableAccessExpression == null)
        {
            var availableVars = string.Join(", ", compileContext.DocumentVariablesParameter.Type.GetFields().Select(f => f.Name));
            throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Variable ${variableName} is not defined in the query variables. Available: [{availableVars}]");
        }

        var val = Expression.Lambda(variableAccessExpression, compileContext.DocumentVariablesParameter!).Compile().DynamicInvoke(compileContext.DocumentVariables);
        return Expression.Constant(val, val?.GetType() ?? typeof(object));
    }
}
