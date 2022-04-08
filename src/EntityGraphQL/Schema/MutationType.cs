using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema;

/// <summary>
/// Wraps up the mutation fields so we can treat this like any other type
/// </summary>
public class MutationType : BaseSchemaTypeWithFields<MutationField>
{
    public override Type TypeDotnet => typeof(MutationType);

    public override bool IsInput => false;

    public override bool IsEnum => false;

    public override bool IsScalar => false;

    public MutationType(ISchemaProvider schema, string name, string? description, RequiredAuthorization? requiredAuthorization)
        : base(schema, name, description, requiredAuthorization)
    {
    }

    /// <summary>
    /// Add any methods marked with GraphQLMutationAttribute in the given object to the schema. Method names are added as using fieldNamer
    /// </summary>
    /// <param name="mutationClassInstance">Instance of a class with mutation methods marked with [GraphQLMutation]</param>
    /// <typeparam name="TType"></typeparam>
    public void AddMutationsFrom<TType>(TType mutationClassInstance) where TType : notnull
    {
        Type type = mutationClassInstance.GetType();
        var classLevelRequiredAuth = schema.AuthorizationService.GetRequiredAuthFromType(type);
        foreach (var method in type.GetMethods())
        {
            if (method.GetCustomAttribute(typeof(GraphQLMutationAttribute)) is GraphQLMutationAttribute attribute)
            {
                var isAsync = method.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) != null;
                string name = schema.SchemaFieldNamer(method.Name);
                var methodAuth = schema.AuthorizationService.GetRequiredAuthFromMember(method);
                var requiredClaims = methodAuth.Concat(classLevelRequiredAuth);
                var actualReturnType = GetTypeFromMutationReturn(isAsync ? method.ReturnType.GetGenericArguments()[0] : method.ReturnType);
                var typeName = schema.GetSchemaType(actualReturnType.GetNonNullableOrEnumerableType(), null).Name;
                var returnType = new GqlTypeInfo(() => schema.Type(typeName), actualReturnType);
                var mutationType = new MutationField(schema, name, returnType, mutationClassInstance, method, attribute.Description, requiredClaims, isAsync, schema.SchemaFieldNamer);

                var obsoleteAttribute = method.GetCustomAttribute<ObsoleteAttribute>();
                if (obsoleteAttribute != null)
                {
                    mutationType.IsDeprecated = true;
                    mutationType.DeprecationReason = obsoleteAttribute.Message;
                }

                fieldsByName[name] = mutationType;
            }
        }
    }

    /// <summary>
    /// Return the actual return type of a mutation - strips out the Expression<Func<>>
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private Type GetTypeFromMutationReturn(Type type)
    {
        if (type.BaseType == typeof(LambdaExpression))
        {
            // This should be Expression<Func<Context, ReturnType>>
            type = type.GetGenericArguments()[0].GetGenericArguments()[1];
        }

        return type;
    }

    public override ISchemaType AddAllFields(bool autoCreateNewComplexTypes = false, bool autoCreateEnumTypes = true)
    {
        return this;
    }
}