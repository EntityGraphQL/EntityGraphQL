using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.EntityQuery.Grammar;

public class IdentityExpression(string name, EqlCompileContext compileContext) : IExpression
{
    public Type Type => throw new NotImplementedException();

    public string Name { get; } = name;

    public Expression Compile(Expression? context, ISchemaProvider? schema, QueryRequestContext requestContext, IMethodProvider methodProvider)
    {
        return MakePropertyCall(context!, schema, Name, requestContext, compileContext);
    }

    internal static Expression MakePropertyCall(Expression context, ISchemaProvider? schema, string name, QueryRequestContext requestContext, EqlCompileContext compileContext)
    {
        if (schema == null)
        {
            try
            {
                return Expression.PropertyOrField(context!, name);
            }
            catch (Exception)
            {
                return MakeConstantFromIdentity(context, schema, name, requestContext);
            }
        }
        // we have a schema we follow it for fields etc
        var schemaType = schema.GetSchemaType(context.Type, false, requestContext);

        if (!schemaType.HasField(name, requestContext))
        {
            return MakeConstantFromIdentity(context, schema, name, requestContext);
        }

        if (schemaType.IsEnum)
        {
            return Expression.Constant(Enum.Parse(schemaType.TypeDotnet, name));
        }
        var gqlField = schemaType.GetField(name, requestContext);

        var isServiceField = gqlField.Services.Count > 0;

        Expression? exp;

        if (isServiceField && compileContext.ExecutionOptions.ExecuteServiceFieldsSeparately)
            // null context so the service expression param is not replaced and we can replace it in the filter extension
            (exp, _) = gqlField.GetExpression(gqlField.ResolveExpression!, null, null, null, compileContext, new Dictionary<string, object?>(), null, null, [], false, new Util.ParameterReplacer());
        else
            (exp, _) = gqlField.GetExpression(gqlField.ResolveExpression!, context, null, null, compileContext, new Dictionary<string, object?>(), null, null, [], false, new Util.ParameterReplacer());

        // Track service-backed fields for later filter splitting and wrap with a marker only
        // when we are executing in split mode (EF pass + services pass).
        if (isServiceField && exp != null && compileContext.ExecutionOptions.ExecuteServiceFieldsSeparately)
        {
            // Create a unique key and store the extracted fields for this service field on the compile context
            if (gqlField.ExtractedFieldsFromServices != null)
            {
                compileContext.ServiceFieldDependencies.Add(gqlField);
                compileContext.OriginalContext = context;
            }

            var marker = typeof(Util.ServiceExpressionMarker).GetMethod(nameof(Util.ServiceExpressionMarker.MarkService))!.MakeGenericMethod(exp.Type);
            exp = Expression.Call(marker, exp);
        }

        return exp!;
    }

    private static Expression MakeConstantFromIdentity(Expression context, ISchemaProvider? schema, string name, QueryRequestContext requestContext)
    {
        var enumField = schema!.GetEnumTypes().Select(e => e.GetFields().FirstOrDefault(f => f.Name == name)).Where(f => f != null).FirstOrDefault();
        if (enumField != null)
        {
            var constExp = Expression.Constant(Enum.Parse(enumField.ReturnType.TypeDotnet, enumField.Name));
            if (constExp != null)
                return constExp;
        }
        if (schema.HasType(name))
        {
            var type = schema.GetSchemaType(name, requestContext);
            if (type.IsEnum)
            {
                return Expression.Default(type.TypeDotnet);
            }
        }

        throw new EntityGraphQLException(GraphQLErrorCategory.DocumentError, $"Field '{name}' not found on type '{schema?.GetSchemaType(context!.Type, false, null)?.Name ?? context!.Type.Name}'");
    }
}
