using System;
using System.Reflection;
using EntityGraphQL.Compiler;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema
{
    public class SubscriptionField : MethodField
    {
        public override GraphQLQueryFieldType FieldType { get; } = GraphQLQueryFieldType.Subscription;

        public SubscriptionField(ISchemaProvider schema, string methodName, GqlTypeInfo returnType, MethodInfo method, string description, RequiredAuthorization requiredAuth, bool isAsync, Func<string, string> fieldNamer, SchemaBuilderMethodOptions options)
            : base(schema, methodName, returnType, method, description, requiredAuth, isAsync, fieldNamer, options)
        {
            if (!method.ReturnType.ImplementsGenericInterface(typeof(IObservable<>)))
                throw new EntityGraphQLCompilerException($"Subscription {methodName} should return an IObservable<>");
        }
    }
}