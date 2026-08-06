using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

public class GraphQLExtractedField : BaseGraphQLField
{
    private readonly ParameterExpression originalParam;
    public IEnumerable<Expression> FieldExpressions { get; }

    public GraphQLExtractedField(ISchemaProvider schema, string name, IEnumerable<Expression> fieldExpressions, ParameterExpression originalParam)
        : base(schema, null, name.Replace(" ", "").Replace(",", ""), null, null, null, null)
    {
        this.originalParam = originalParam;
        this.FieldExpressions = fieldExpressions;
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
        if (withoutServiceFields)
        {
            // we don't need any other expression as they are the same. We just need to make sure we select the data
            var fieldExp = FieldExpressions!.First();
            if (replacementNextFieldContext != null)
            {
                var newParam = replacementNextFieldContext.Type == originalParam.Type ? replacementNextFieldContext : Expression.Convert(replacementNextFieldContext, originalParam.Type);
                fieldExp = replacer.Replace(fieldExp, originalParam, newParam);
            }
            return fieldExp;
        }
        return GetNodeExpression(replacementNextFieldContext!, possibleNextContextTypes);
    }

    public Expression GetNodeExpression(Expression replacementNextFieldContext, List<Type>? possibleNextContextTypes)
    {
        if (replacementNextFieldContext.Type.GetProperty(Name) != null || replacementNextFieldContext.Type.GetField(Name) != null)
        {
            // extracted fields get flatten as they are selected in the first pass. The new expression can be built
            return Expression.PropertyOrField(replacementNextFieldContext, Name);
        }
        else if (possibleNextContextTypes != null)
        {
            foreach (var type in possibleNextContextTypes)
            {
                if (type.GetProperty(Name) != null || type.GetField(Name) != null)
                {
                    return Expression.PropertyOrField(Expression.Convert(replacementNextFieldContext!, type), Name);
                }
            }
        }
        // Not flattened into the context we were handed, and that context is the entity this dependency was
        // extracted from - so read it straight off that instead. It happens where a field is built in one go on
        // the services pass rather than split across the two: an interleaved paging field (its resolver uses a
        // service inside the expression, so it cannot be split) builds its items from the entity, so a service
        // field selected on those items finds no first-pass projection to take its dependency from.
        if (replacementNextFieldContext.Type == originalParam.Type || originalParam.Type.IsAssignableFrom(replacementNextFieldContext.Type))
            return new ParameterReplacer().Replace(FieldExpressions.First(), originalParam, replacementNextFieldContext);

        throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Could not find field {Name} on type {replacementNextFieldContext.Type.Name}");
    }
}
