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
    /// <param name="options">Options for the schema builder</param>
    /// <typeparam name="TType"></typeparam>
    public MutationType AddFrom<TType>(SchemaBuilderMutationOptions? options = null) where TType : class
    {
        options ??= new SchemaBuilderMutationOptions();
        var types = typeof(TType).Assembly
                                .GetTypes()
                                .Where(x => x.IsClass && !x.IsAbstract)
                                .Where(x => typeof(TType).IsAssignableFrom(x));

        foreach (Type type in types)
        {
            var classLevelRequiredAuth = SchemaType.Schema.AuthorizationService.GetRequiredAuthFromType(type);
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var attribute = method.GetCustomAttribute(typeof(GraphQLMutationAttribute)) as GraphQLMutationAttribute;
                if (attribute != null || options.AddNonAttributedMethods)
                {
                    string name = SchemaType.Schema.SchemaFieldNamer(method.Name);
                    AddMutationMethod(name, classLevelRequiredAuth, method, attribute?.Description ?? "", options);
                }
            }
        }
        return this;
    }

    /// <summary>
    /// Add a single mutation field to the schema. Mutation field name will be generated from the method 
    /// name using the SchemaFieldNamer. Use the [Description] attribute on the method to set the description.
    /// </summary>
    /// <param name="mutationDelegate">A method to execute the mutation logic</param>
    /// <param name="options">Options for the schema builder</param>
    public MutationField Add(Delegate mutationDelegate, SchemaBuilderMutationOptions? options = null)
    {
        return Add(SchemaType.Schema.SchemaFieldNamer(mutationDelegate.Method.Name), mutationDelegate, options);
    }

    /// <summary>
    /// Add a single mutation field to the schema. Mutation field name used as supplied
    /// </summary>
    /// <param name="mutationName">Mutation field name</param>
    /// <param name="mutationDelegate">A method to execute the mutation logic</param>
    /// <param name="options">Options for the schema builder</param>
    public MutationField Add(string mutationName, Delegate mutationDelegate, SchemaBuilderMutationOptions? options = null)
    {
        var description = (mutationDelegate.Method.GetCustomAttribute(typeof(DescriptionAttribute)) as DescriptionAttribute)?.Description ?? string.Empty;
        return Add(mutationName, description, mutationDelegate, options);
    }

    /// <summary>
    /// Add a single mutation field to the schema. Mutation field name used as supplied.
    /// </summary>
    /// <param name="mutationName">Mutation field name</param>
    /// <param name="description">Description of the mutation field</param>
    /// <param name="mutationDelegate">A method to execute the mutation logic</param>
    /// <param name="options">Options for the schema builder</param>
    public MutationField Add(string mutationName, string description, Delegate mutationDelegate, SchemaBuilderMutationOptions? options = null)
    {
        return AddMutationMethod(mutationName, null, mutationDelegate.Method, description, options);
    }

    private MutationField AddMutationMethod(string name, RequiredAuthorization? classLevelRequiredAuth, MethodInfo method, string? description, SchemaBuilderMutationOptions? options)
    {
        options ??= new SchemaBuilderMutationOptions();
        var isAsync = method.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) != null;
        var takeGenericArgument = isAsync || method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>);
        var methodAuth = SchemaType.Schema.AuthorizationService.GetRequiredAuthFromMember(method);
        var requiredClaims = methodAuth;
        if (classLevelRequiredAuth != null)
            requiredClaims = requiredClaims.Concat(classLevelRequiredAuth);
        var actualReturnType = GetTypeFromMutationReturn(takeGenericArgument ? method.ReturnType.GetGenericArguments()[0] : method.ReturnType);
        var nonListReturnType = actualReturnType.GetNonNullableOrEnumerableType();
        if (!SchemaType.Schema.HasType(nonListReturnType) && options.AutoCreateNewComplexTypes)
        {
            SchemaBuilder.CacheType(nonListReturnType, SchemaType.Schema, options, false);
        }
        var typeName = SchemaType.Schema.GetSchemaType(nonListReturnType, null).Name;
        var returnType = new GqlTypeInfo(() => SchemaType.Schema.Type(typeName), actualReturnType, method.IsNullable());
        var mutationField = new MutationField(SchemaType.Schema, name, returnType, method, description ?? string.Empty, requiredClaims, isAsync, SchemaType.Schema.SchemaFieldNamer, options);

        var validators = method.GetCustomAttributes<ArgumentValidatorAttribute>();
        if (validators != null)
        {
            foreach (var validator in validators)
            {
                mutationField.AddValidator(validator.Validator.ValidateAsync);
            }
        }

        var obsoleteAttribute = method.GetCustomAttribute<ObsoleteAttribute>();
        if (obsoleteAttribute != null)
        {
            mutationField.IsDeprecated = true;
            mutationField.DeprecationReason = obsoleteAttribute.Message;
        }

        SchemaType.FieldsByName[name] = mutationField;
        return mutationField;
    }

    /// <summary>
    /// Return the actual return type of a mutation - strips out the Expression<Func<>>
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static Type GetTypeFromMutationReturn(Type type)
    {
        if (type.BaseType == typeof(LambdaExpression))
        {
            // This should be Expression<Func<Context, ReturnType>>
            type = type.GetGenericArguments()[0].GetGenericArguments()[1];
        }

        return type;
    }
}
