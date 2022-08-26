using System;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using EntityGraphQL.Extensions;

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
        if (type.BaseType == typeof(LambdaExpression))
        {
            // This should be Expression<Func<Context, ReturnType>>
            type = type.GetGenericArguments()[0].GetGenericArguments()[1];
        }
        if (isAsync)
        {
            type = type.GetGenericArguments()[0];
        }
        return type;
    }
}

protected override BaseField MakeField(string name, MethodInfo method, string? description, SchemaBuilderMethodOptions? options, bool isAsync, RequiredAuthorization requiredClaims, GqlTypeInfo returnType)
{
    options ??= new SchemaBuilderMethodOptions();
    return new MutationField(SchemaType.Schema, name, returnType, method, description ?? string.Empty, requiredClaims, isAsync, SchemaType.Schema.SchemaFieldNamer, options);
}
}
