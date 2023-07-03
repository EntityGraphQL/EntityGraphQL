using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using EntityGraphQL.Extensions;
using Nullability;

namespace EntityGraphQL.Schema;

/// <summary>
/// Provides the interface to add/modify a schema type based on a controller style class with methods Specifically Mutations & Subscriptions
/// </summary>
public abstract class ControllerType
{
    public ISchemaType SchemaType { get; }

    public ControllerType(ISchemaType schemaType)
    {
        SchemaType = schemaType;
    }

    /// <summary>
    /// Add any public methods (static or not) marked with GraphQLMethodAttribute (GraphQLMutationAttribute or GraphQLSubscriptionAttribute) in the given object to the schema. Method names are added as using fieldNamer
    /// </summary>
    /// <param name="options">Options for the schema builder</param>
    /// <typeparam name="TType"></typeparam>
    public ControllerType AddFrom<TType>(SchemaBuilderOptions? options = null) where TType : class
    {
        options ??= new SchemaBuilderOptions();
        var types = typeof(TType).Assembly
                                .GetTypes()
                                .Where(x => x.IsClass && !x.IsAbstract)
                                .Where(x => typeof(TType).IsAssignableFrom(x));

        foreach (Type type in types)
        {
            var classLevelRequiredAuth = SchemaType.Schema.AuthorizationService.GetRequiredAuthFromType(type);
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var attribute = method.GetCustomAttribute(typeof(GraphQLMethodAttribute)) as GraphQLMethodAttribute;
                if (attribute != null || options.AddNonAttributedMethodsInControllers)
                {
                    string name = SchemaType.Schema.SchemaFieldNamer(method.Name);
                    AddMethodAsField(name, classLevelRequiredAuth, method, attribute?.Description ?? "", options);
                }
            }
        }
        return this;
    }

    /// <summary>
    /// Add a single field to the schema. Field name will be generated from the method 
    /// name using the SchemaFieldNamer. Use the [Description] attribute on the method to set the description.
    /// </summary>
    /// <param name="delegate">A method to execute the logic</param>
    /// <param name="options">Options for the schema builder</param>
    public BaseField Add(Delegate @delegate, SchemaBuilderOptions? options = null)
    {
        return Add(SchemaType.Schema.SchemaFieldNamer(@delegate.Method.Name), @delegate, options);
    }

    /// <summary>
    /// Add a single field to the schema. Field name used as supplied
    /// </summary>
    /// <param name="fieldName">Field name</param>
    /// <param name="delegate">A method to execute the logic</param>
    /// <param name="options">Options for the schema builder</param>
    public BaseField Add(string fieldName, Delegate @delegate, SchemaBuilderOptions? options = null)
    {
        var description = (@delegate.Method.GetCustomAttribute(typeof(DescriptionAttribute)) as DescriptionAttribute)?.Description ?? string.Empty;
        return Add(fieldName, description, @delegate, options);
    }

    /// <summary>
    /// Add a single field to the schema. Field name used as supplied.
    /// </summary>
    /// <param name="fieldName">Field name</param>
    /// <param name="description">Description of the field</param>
    /// <param name="delegate">A method to execute the logic</param>
    /// <param name="options">Options for the schema builder</param>
    public BaseField Add(string fieldName, string description, Delegate @delegate, SchemaBuilderOptions? options = null)
    {
        return AddMethodAsField(fieldName, null, @delegate.Method, description, options);
    }

    private BaseField AddMethodAsField(string name, RequiredAuthorization? classLevelRequiredAuth, MethodInfo method, string? description, SchemaBuilderOptions? options)
    {
        options ??= new SchemaBuilderOptions();
        var isAsync = method.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) != null || (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
        var methodAuth = SchemaType.Schema.AuthorizationService.GetRequiredAuthFromMember(method);
        var requiredClaims = methodAuth;
        if (classLevelRequiredAuth != null)
            requiredClaims = requiredClaims.Concat(classLevelRequiredAuth);
        var actualReturnType = GetTypeFromMethodReturn(method.ReturnType, isAsync);
        var nonListReturnType = actualReturnType.IsEnumerableOrArray() ? actualReturnType.GetNonNullableOrEnumerableType() : actualReturnType;
        if (!SchemaType.Schema.HasType(nonListReturnType) && options.AutoCreateNewComplexTypes)
        {
            SchemaBuilder.CacheType(nonListReturnType, SchemaType.Schema, options, false);
        }
        var typeName = SchemaType.Schema.GetSchemaType(nonListReturnType, null).Name;

        var nullability = method.GetNullabilityInfo().Unwrap();
        var returnType = new GqlTypeInfo(() => SchemaType.Schema.Type(typeName), actualReturnType, nullability);
        var field = MakeField(name, method, description, options, isAsync, requiredClaims, returnType);

        field.ApplyAttributes(method.GetCustomAttributes());

        // add the subscription/mutation type if it doesn't already exist
        if (!SchemaType.Schema.HasType(SchemaType.TypeDotnet))
            SchemaType.Schema.AddType(SchemaType);

        SchemaType.AddField(field);
        return field;
    }

    protected abstract BaseField MakeField(string name, MethodInfo method, string? description, SchemaBuilderOptions? options, bool isAsync, RequiredAuthorization requiredClaims, GqlTypeInfo returnType);

    /// <summary>
    /// Return the actual return type of the field - may strip out the Expression<Func<>> etc depedning on the implementation
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    protected abstract Type GetTypeFromMethodReturn(Type type, bool isAsync);
}
