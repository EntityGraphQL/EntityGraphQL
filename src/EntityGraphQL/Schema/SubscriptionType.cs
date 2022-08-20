using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema;

public class SubscriptionType
{
    public ISchemaType SchemaType { get; }
    public SubscriptionType(ISchemaProvider schema, string name)
    {
        SchemaType = new SchemaType<SubscriptionType>(schema, name, null, null);
    }

    /// <summary>
    /// Add any public methods (static or not) in the given object to the schema. Method names are added as using fieldNamer
    /// </summary>
    /// <param name="autoAddInputTypes">If true, any class types seen in the subscription argument properties will be added to the schema</param>
    /// <param name="addNonAttributedMethods">Default true. If false only methods with the GraphQLSubscriptionAttribute will be added</param>
    /// <typeparam name="TType"></typeparam>
    public SubscriptionType AddFrom<TType>(bool autoAddInputTypes = false, bool addNonAttributedMethods = true) where TType : class
    {
        var types = typeof(TType).Assembly
                            .GetTypes()
                            .Where(x => x.IsClass && !x.IsAbstract)
                            .Where(x => typeof(TType).IsAssignableFrom(x));

        foreach (Type type in types)
        {
            var classLevelRequiredAuth = SchemaType.Schema.AuthorizationService.GetRequiredAuthFromType(type);
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var attribute = method.GetCustomAttribute(typeof(GraphQLSubscriptionAttribute)) as GraphQLSubscriptionAttribute;
                if (attribute != null || addNonAttributedMethods)
                {
                    string name = SchemaType.Schema.SchemaFieldNamer(method.Name);
                    AddSubscriptionMethod(name, classLevelRequiredAuth, method, attribute?.Description ?? "", autoAddInputTypes);
                }
            }
        }
        return this;
    }

    /// <summary>
    /// Add a single subscription field to the schema. Subscription field name will be generated from the method 
    /// name using the SchemaFieldNamer. Use the [Description] attribute on the method to set the description.
    /// </summary>
    /// <param name="subscriptionDelegate">A method to execute the subscription logic</param>
    /// <param name="autoAddInputTypes">If true, any class types seen in the subscription argument properties will be added to the schema</param>
    public SubscriptionField Add(Delegate subscriptionDelegate, bool autoAddInputTypes = false)
    {
        return Add(SchemaType.Schema.SchemaFieldNamer(subscriptionDelegate.Method.Name), subscriptionDelegate, autoAddInputTypes);
    }

    /// <summary>
    /// Add a single subscription field to the schema. Subscription field name used as supplied
    /// </summary>
    /// <param name="subscriptionName">Subscription field name</param>
    /// <param name="subscriptionDelegate">A method to execute the subscription logic</param>
    /// <param name="autoAddInputTypes">If true, any class types seen in the subscription argument properties will be added to the schema</param>
    public SubscriptionField Add(string subscriptionName, Delegate subscriptionDelegate, bool autoAddInputTypes = false)
    {
        var description = (subscriptionDelegate.Method.GetCustomAttribute(typeof(DescriptionAttribute)) as DescriptionAttribute)?.Description ?? string.Empty;
        return Add(subscriptionName, description, subscriptionDelegate, autoAddInputTypes);
    }

    /// <summary>
    /// Add a single subscription field to the schema. Subscription field name used as supplied.
    /// </summary>
    /// <param name="subscriptionName">Subscription field name</param>
    /// <param name="subscriptionDelegate">A method to execute the subscription logic</param>
    /// <param name="autoAddInputTypes">If true, any class types seen in the subscription argument properties will be added to the schema</param>
    public SubscriptionField Add(string subscriptionName, string description, Delegate subscriptionDelegate, bool autoAddInputTypes = false)
    {
        return AddSubscriptionMethod(subscriptionName, null, subscriptionDelegate.Method, description, autoAddInputTypes);
    }

    private SubscriptionField AddSubscriptionMethod(string name, RequiredAuthorization? classLevelRequiredAuth, MethodInfo method, string? description, bool autoAddInputTypes)
    {
        var isAsync = method.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) != null;
        var methodAuth = SchemaType.Schema.AuthorizationService.GetRequiredAuthFromMember(method);
        var requiredClaims = methodAuth;
        if (classLevelRequiredAuth != null)
            requiredClaims = requiredClaims.Concat(classLevelRequiredAuth);
        var actualReturnType = isAsync || method.ReturnType.GetGenericTypeDefinition() == typeof(IObservable<>) ? method.ReturnType.GetGenericArguments()[0] : method.ReturnType;
        var typeName = SchemaType.Schema.GetSchemaType(actualReturnType.GetNonNullableOrEnumerableType(), null).Name;
        var returnType = new GqlTypeInfo(() => SchemaType.Schema.Type(typeName), actualReturnType, method.IsNullable());
        var subscriptionField = new SubscriptionField(SchemaType.Schema, name, returnType, method, description ?? string.Empty, requiredClaims, isAsync, SchemaType.Schema.SchemaFieldNamer, autoAddInputTypes);

        var validators = method.GetCustomAttributes<ArgumentValidatorAttribute>();
        if (validators != null)
        {
            foreach (var validator in validators)
            {
                subscriptionField.AddValidator(validator.Validator.ValidateAsync);
            }
        }

        var obsoleteAttribute = method.GetCustomAttribute<ObsoleteAttribute>();
        if (obsoleteAttribute != null)
        {
            subscriptionField.IsDeprecated = true;
            subscriptionField.DeprecationReason = obsoleteAttribute.Message;
        }

        // add the subscription type if it doesn't already exist
        if (!SchemaType.Schema.HasType(SchemaType.TypeDotnet))
            SchemaType.Schema.AddType(SchemaType);

        SchemaType.AddField(subscriptionField);
        return subscriptionField;
    }
}