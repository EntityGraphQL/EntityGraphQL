using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace EntityGraphQL.Schema;

/// <summary>
/// Provides the interface to add/modify the mutation schema type
/// </summary>
public class MutationType : ControllerType
{
    public MutationType(ISchemaProvider schema, string name, string? description, RequiredAuthorization? requiredAuthorization)
        : base(new MutationSchemaType(schema, name, description, requiredAuthorization))
    {
    }

    protected override Type GetTypeFromMethodReturn(Type type, bool isAsync)
    {
        if (isAsync || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>)))
        {
            type = type.GetGenericArguments()[0];
        }
        if (type.BaseType == typeof(LambdaExpression))
        {
            // This should be Expression<Func<Context, ReturnType>>
            type = type.GetGenericArguments()[0].GetGenericArguments()[1];
        }
        return type;
    }

    protected override BaseField MakeField(string name, MethodInfo method, string? description, SchemaBuilderOptions? options, bool isAsync, RequiredAuthorization requiredClaims, GqlTypeInfo returnType)
    {
        options ??= new SchemaBuilderOptions();
        return new MutationField(SchemaType.Schema, SchemaType, name, returnType, method, description ?? string.Empty, requiredClaims, isAsync, options);
    }
}
