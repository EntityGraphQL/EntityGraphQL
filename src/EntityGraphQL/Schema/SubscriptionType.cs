using System;
using System.Linq;
using System.Linq.Expressions;
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
        // IObservable<Expression<Func<QueryContext, ReturnType>>>
        if (type.ImplementsGenericInterface(typeof(Expression<>)))
        {
            type = type.GetGenericArgument(typeof(Expression<>))!.GetGenericArguments().Last();
        }
        return type;
    }

    protected override BaseField MakeField(string name, MethodInfo method, string? description, SchemaBuilderOptions? options, bool isAsync, RequiredAuthorization requiredClaims, GqlTypeInfo returnType)
    {
        options ??= new SchemaBuilderOptions();
        return new SubscriptionField(SchemaType.Schema, SchemaType, name, returnType, method, description ?? string.Empty, requiredClaims, isAsync, options);

    }
}