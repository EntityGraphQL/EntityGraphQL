using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.Validators;
using Nullability;

namespace EntityGraphQL.Schema;

/// <summary>
/// Holds if a arg has a default value set, which may be null
/// </summary>
public record DefaultArgValue
{
    /// <summary>
    /// True is the argument definition has a default value set.
    /// </summary>
    public bool IsSet { get; set; }
    public object? Value { get; set; }

    public DefaultArgValue(bool isSet, object? value)
    {
        Value = value;
        IsSet = isSet;
    }
}

/// <summary>
/// Holds information about arguments for fields
/// </summary>
public class ArgType
{
    public string Name { get; private set; }
    public string DotnetName { get; private set; }
    public string Description { get; set; }
    public GqlTypeInfo Type { get; private set; }
    public DefaultArgValue DefaultValue { get; set; }
    private RequiredAttribute? requiredAttribute;
    public bool IsRequired { get; set; }
    public Type RawType { get; private set; }

    public ArgType(string name, string dotnetName, GqlTypeInfo type, Type rawType)
    {
        Name = name;
        DotnetName = dotnetName;
        Description = string.Empty;
        Type = type;
        RawType = rawType;
        DefaultValue = new DefaultArgValue(false, null);
        IsRequired = false;
    }

    public static ArgType FromProperty(ISchemaProvider schema, PropertyInfo prop, DefaultArgValue defaultValue)
    {
        var nullability = prop.GetNullabilityInfo();
        var arg = MakeArgType(schema, prop.Name, prop, prop.GetCustomAttributes(), prop.PropertyType, defaultValue, nullability);

        return arg;
    }

    public static ArgType FromParameter(ISchemaProvider schema, ParameterInfo parameter, DefaultArgValue defaultValue)
    {
        var nullability = parameter.GetNullabilityInfo();
        var arg = MakeArgType(schema, parameter.Name!, null, parameter.GetCustomAttributes(), parameter.ParameterType, defaultValue, nullability);
        return arg;
    }

    public static ArgType FromField(ISchemaProvider schema, FieldInfo field, DefaultArgValue defaultValue)
    {
        var nullability = field.GetNullabilityInfo();
        var arg = MakeArgType(schema, field.Name, field, field.GetCustomAttributes(), field.FieldType, defaultValue, nullability);
        return arg;
    }

    private static ArgType MakeArgType(
        ISchemaProvider schema,
        string name,
        MemberInfo? memberInfo,
        IEnumerable<Attribute> attributes,
        Type type,
        DefaultArgValue defaultValue,
        NullabilityInfo nullability
    )
    {
        var markedRequired = false;
        var gqlLookupType = type;
        var argType = type;
        if (gqlLookupType.IsConstructedGenericType && gqlLookupType.GetGenericTypeDefinition() == typeof(RequiredField<>))
        {
            markedRequired = true;
            argType = gqlLookupType = gqlLookupType.GetGenericArguments()[0];
            // default value will often be the default value of the non-null type (e.g. 0 for int).
            // We are saying here it must be provided by the query
            defaultValue.Value = null;
            defaultValue.IsSet = false;
        }
        if (gqlLookupType.IsConstructedGenericType && gqlLookupType.GetGenericTypeDefinition() == typeof(EntityQueryType<>))
        {
            gqlLookupType = typeof(string);
        }
        if (gqlLookupType.IsEnumerableOrArray())
        {
            gqlLookupType = gqlLookupType.GetNonNullableOrEnumerableType();
        }
        if (gqlLookupType.IsNullableType())
        {
            gqlLookupType = gqlLookupType.GetGenericArguments()[0];
        }

        var gqlTypeInfo = new GqlTypeInfo(() => schema.GetSchemaType(gqlLookupType, true, null), argType, nullability);
        var arg = new ArgType(schema.SchemaFieldNamer(name), name, gqlTypeInfo, type)
        {
            DefaultValue = defaultValue,
            IsRequired = markedRequired,
            requiredAttribute = attributes.FirstOrDefault(a => a is RequiredAttribute) as RequiredAttribute,
        };

        if (memberInfo?.GetCustomAttribute<GraphQLFieldAttribute>() is GraphQLFieldAttribute gqlFieldAttr)
        {
            if (!string.IsNullOrEmpty(gqlFieldAttr.Name))
                arg.Name = gqlFieldAttr.Name;
            if (!string.IsNullOrEmpty(gqlFieldAttr.Description))
                arg.Description = gqlFieldAttr.Description;
        }

        if (arg.requiredAttribute != null || GraphQLNotNullAttribute.IsMemberMarkedNotNull(attributes) || nullability.WriteState == NullabilityState.NotNull)
        {
            arg.IsRequired = true;
        }

        if (arg.IsRequired)
            arg.Type.TypeNotNullable = true;
        else if (arg.Type.TypeNotNullable)
            arg.IsRequired = true;

        if (attributes.FirstOrDefault(a => a is DescriptionAttribute) is DescriptionAttribute d)
        {
            arg.Description = d.Description;
        }

        return arg;
    }

    /// <summary>
    /// Validate that the value for the argument meets the requirements of the argument
    /// </summary>
    /// <param name="val"></param>
    /// <param name="field"></param>
    /// <param name="validationErrors"></param>
    public async Task ValidateAsync(object? val, IField field, List<string> validationErrors)
    {
        var valType = val?.GetType();
        if (valType != null && valType.IsGenericType && valType.GetGenericTypeDefinition() == typeof(RequiredField<>))
            val = valType.GetProperty("Value")!.GetValue(val);
        if (requiredAttribute != null && !requiredAttribute.IsValid(val))
            validationErrors.Add(requiredAttribute.ErrorMessage != null ? $"Field '{field.Name}' - {requiredAttribute.ErrorMessage}" : $"Field '{field.Name}' - missing required argument '{Name}'");
        else if (IsRequired && val == null && !DefaultValue.IsSet)
            validationErrors.Add($"Field '{field.Name}' - missing required argument '{Name}'");

        // Validate using all DataAnnotations validation attributes on the member
        if (Type.SchemaType.IsInput)
        {
            // For input types, validate the entire object and its properties recursively
            var validator = new DataAnnotationsValidator();
            var context = new ArgumentValidatorContext(field, val);

            await validator.ValidateAsync(context);
            validationErrors.AddRange(context.Errors);
        }

        Type.SchemaType.Validate(val);
    }
}
