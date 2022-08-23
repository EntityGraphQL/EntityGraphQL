using System;
using System.Reflection;

namespace EntityGraphQL.Schema
{
    public class MutationField : MethodField
    {
        public override GraphQLQueryFieldType FieldType { get; } = GraphQLQueryFieldType.Mutation;

        public MutationField(ISchemaProvider schema, string methodName, GqlTypeInfo returnType, MethodInfo method, string description, RequiredAuthorization requiredAuth, bool isAsync, Func<string, string> fieldNamer, SchemaBuilderMethodOptions options)
            : base(schema, methodName, returnType, method, description, requiredAuth, isAsync, fieldNamer, options)
        {
        }
    }
}