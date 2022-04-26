using System;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema;

/// <summary>
/// Provides the interface to add/modify the mutation schema type
/// </summary>
public class MutationType
{
    public MutationSchemaType SchemaType { get; }

    public MutationType(ISchemaProvider schema, string name, string? description, RequiredAuthorization? requiredAuthorization)
    {
        SchemaType = new MutationSchemaType(schema, name, description, requiredAuthorization);
    }

    /// <summary>
    /// Add any public methods (static or not) marked with GraphQLMutationAttribute in the given object to the schema. Method names are added as using fieldNamer
    /// </summary>
    /// <param name="mutationClassInstance">Instance of a class with mutation methods marked with [GraphQLMutation]</param>
    /// <typeparam name="TType"></typeparam>
    public void AddFrom<TType>(TType mutationClassInstance) where TType : notnull
    {
        Type type = mutationClassInstance.GetType();
        var classLevelRequiredAuth = SchemaType.Schema.AuthorizationService.GetRequiredAuthFromType(type);
        foreach (var method in type.GetMethods())
        {
            if (method.GetCustomAttribute(typeof(GraphQLMutationAttribute)) is GraphQLMutationAttribute attribute)
            {
                string name = SchemaType.Schema.SchemaFieldNamer(method.Name);
                AddMutationMethod(name, mutationClassInstance, classLevelRequiredAuth, method, attribute.Description);
            }
        }
    }

    /// <summary>
    /// Add a single mutation field to the schema. Mutation field name will be generated from the method 
    /// name using the SchemaFieldNamer. Use the [Description] attribute on the method to set the description.
    /// </summary>
    /// <param name="mutationDelegate">A method to execute the mutation logic</param>
    public void Add(Delegate mutationDelegate)
    {
        Add(SchemaType.Schema.SchemaFieldNamer(mutationDelegate.Method.Name), mutationDelegate);
    }

    /// <summary>
    /// Add a single mutation field to the schema. Mutation field name used as supplied
    /// </summary>
    /// <param name="mutationName">Mutation field name</param>
    /// <param name="mutationDelegate">A method to execute the mutation logic</param>
    public void Add(string mutationName, Delegate mutationDelegate)
    {
        var description = (mutationDelegate.Method.GetCustomAttribute(typeof(DescriptionAttribute)) as DescriptionAttribute)?.Description ?? string.Empty;
        Add(mutationName, description, mutationDelegate);
    }

    /// <summary>
    /// Add a single mutation field to the schema. Mutation field name used as supplied.
    /// </summary>
    /// <param name="mutationName">Mutation field name</param>
    /// <param name="mutationDelegate">A method to execute the mutation logic</param>
    public void Add(string mutationName, string description, Delegate mutationDelegate)
    {
        AddMutationMethod(mutationName, mutationDelegate.Target, null, mutationDelegate.Method, description);
    }

    private void AddMutationMethod<TType>(string name, TType mutationClassInstance, RequiredAuthorization? classLevelRequiredAuth, MethodInfo method, string? description) where TType : notnull
    {
        var isAsync = method.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) != null;
        var methodAuth = SchemaType.Schema.AuthorizationService.GetRequiredAuthFromMember(method);
        var requiredClaims = methodAuth;
        if (classLevelRequiredAuth != null)
            requiredClaims = requiredClaims.Concat(classLevelRequiredAuth);
        var actualReturnType = GetTypeFromMutationReturn(isAsync ? method.ReturnType.GetGenericArguments()[0] : method.ReturnType);
        var typeName = SchemaType.Schema.GetSchemaType(actualReturnType.GetNonNullableOrEnumerableType(), null).Name;
        var returnType = new GqlTypeInfo(() => SchemaType.Schema.Type(typeName), actualReturnType);
        var mutationType = new MutationField(SchemaType.Schema, name, returnType, mutationClassInstance, method, description ?? string.Empty, requiredClaims, isAsync, SchemaType.Schema.SchemaFieldNamer);

        var obsoleteAttribute = method.GetCustomAttribute<ObsoleteAttribute>();
        if (obsoleteAttribute != null)
        {
            mutationType.IsDeprecated = true;
            mutationType.DeprecationReason = obsoleteAttribute.Message;
        }

        SchemaType.FieldsByName[name] = mutationType;
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
}

/// <summary>
/// Wraps up the mutation fields so we can treat this like any other type
/// </summary>
public class MutationSchemaType : BaseSchemaTypeWithFields<MutationField>
{
    public override Type TypeDotnet => typeof(MutationType);

    public override bool IsInput => false;

    public override bool IsEnum => false;

    public override bool IsScalar => false;

    public MutationSchemaType(ISchemaProvider schema, string name, string? description, RequiredAuthorization? requiredAuthorization)
        : base(schema, name, description, requiredAuthorization)
    {
    }

    public override ISchemaType AddAllFields(bool autoCreateNewComplexTypes = false, bool autoCreateEnumTypes = true)
    {
        return this;
    }
}