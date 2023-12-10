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

    protected override Expression GetFieldExpression(CompileContext compileContext, IServiceProvider? serviceProvider, List<GraphQLFragmentStatement> fragments, ParameterExpression? docParam, object? docVariables, ParameterExpression schemaContext, bool withoutServiceFields, Expression? replacementNextFieldContext, bool isRoot, bool contextChanged, ParameterReplacer replacer)
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
        return GetNodeExpression(replacementNextFieldContext!);
    }

    public Expression GetNodeExpression(Expression replacementNextFieldContext)
    {
        try
        {
            // extracted fields get flatten as they are selected in the first pass. The new expression can be built
            return Expression.PropertyOrField(replacementNextFieldContext!, Name);
        }
        catch (ArgumentException )
        {
            //HACK - works but will be slow
            var subTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsSubclassOf(replacementNextFieldContext.Type));
          
            foreach (var type in subTypes)
            {
                try
                {
                    return Expression.PropertyOrField(Expression.Convert(replacementNextFieldContext!, type), Name);
                }
                catch { }
            }

            throw;
        }
    }
}