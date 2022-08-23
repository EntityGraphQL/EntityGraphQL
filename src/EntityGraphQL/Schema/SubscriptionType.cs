using System;
using System.Reflection;
using System.Threading.Tasks;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema;

public class SubscriptionType : ControllerType
{
    public SubscriptionType(ISchemaProvider schema, string name)
        : base(new SchemaType<SubscriptionType>(schema, name, null, null))
    {
    }

    protected override Type GetTypeFromMethodReturn(Type type, bool isAsync)
    {
        if (isAsync || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>)))
        {
            type = type.GetGenericArguments()[0];
        }
        if (type.ImplementsGenericInterface(typeof(IObservable<>)))
        {
            type = type.GetGenericArgument(typeof(IObservable<>))!;
        }
        return type;
    }

    protected override BaseField MakeField(string name, MethodInfo method, string? description, SchemaBuilderMethodOptions? options, bool isAsync, RequiredAuthorization requiredClaims, GqlTypeInfo returnType)
    {
        options ??= new SchemaBuilderMethodOptions();
        return new SubscriptionField(SchemaType.Schema, name, returnType, method, description ?? string.Empty, requiredClaims, isAsync, SchemaType.Schema.SchemaFieldNamer, options);

    }
}