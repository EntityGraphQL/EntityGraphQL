using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler.EntityQuery.Grammar;

public class IdentityExpression(string name, CompileContext compileContext) : IExpression
{
    public Type Type => throw new NotImplementedException();

    public string Name { get; } = name;

    public Expression Compile(Expression? context, ISchemaProvider? schema, QueryRequestContext requestContext, IMethodProvider methodProvider)
    {
        return MakePropertyCall(context!, schema, Name, requestContext, compileContext);
    }

    internal static Expression MakePropertyCall(Expression context, ISchemaProvider? schema, string name, QueryRequestContext requestContext, CompileContext compileContext)
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
        (var exp, _) = gqlField.GetExpression(gqlField.ResolveExpression!, context, null, null, compileContext, new Dictionary<string, object>(), null, null, [], false, new Util.ParameterReplacer());
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

        throw new EntityGraphQLCompilerException($"Field '{name}' not found on type '{schema?.GetSchemaType(context!.Type, false, null)?.Name ?? context!.Type.Name}'");
    }
}
