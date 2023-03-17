using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using EntityGraphQL.Extensions;
using Nullability;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Holds information about arguments for fields
    /// </summary>
    public class ArgType
    {
        public string Name { get; private set; }
        public string DotnetName { get; private set; }
        public string Description { get; set; }
        public GqlTypeInfo Type { get; private set; }
        public object? DefaultValue { get; set; }
        public MemberInfo? MemberInfo { get; internal set; }

        private RequiredAttribute? requiredAttribute;
        public bool IsRequired { get; set; }
        public Type RawType { get; private set; }

        public ArgType(string name, string dotnetName, GqlTypeInfo type, MemberInfo? memberInfo, Type rawType)
        {
            Name = name;
            DotnetName = dotnetName;
            Description = string.Empty;
            Type = type;
            MemberInfo = memberInfo;
            RawType = rawType;
            DefaultValue = null;
            IsRequired = false;
        }

        public static ArgType FromProperty(ISchemaProvider schema, PropertyInfo prop, object? defaultValue)
        {
            var nullability = prop.GetNullabilityInfo();
            var arg = MakeArgType(schema, prop.Name, prop, prop.GetCustomAttributes(), prop.PropertyType, defaultValue, nullability);

            return arg;
        }

        public static ArgType FromParameter(ISchemaProvider schema, ParameterInfo parameter, object? defaultValue)
        {
            var nullability = parameter.GetNullabilityInfo();
            var arg = MakeArgType(schema, parameter.Name!, parameter.Member, parameter.GetCustomAttributes(), parameter.ParameterType, defaultValue, nullability);
            return arg;
        }

        public static ArgType FromField(ISchemaProvider schema, FieldInfo field, object? defaultValue)
        {
            var nullability = field.GetNullabilityInfo();
            var arg = MakeArgType(schema, field.Name, field, field.GetCustomAttributes(), field.FieldType, defaultValue, nullability);
            return arg;
        }

        private static ArgType MakeArgType(ISchemaProvider schema, string name, MemberInfo? memberInfo, IEnumerable<Attribute> attributes, Type type, object? defaultValue, NullabilityInfo nullability)
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
                defaultValue = null;
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

            var gqlTypeInfo = new GqlTypeInfo(() => schema.GetSchemaType(gqlLookupType, null), argType, nullability);
            var arg = new ArgType(schema.SchemaFieldNamer(name), name, gqlTypeInfo, memberInfo, type)
            {
                DefaultValue = defaultValue,
                IsRequired = markedRequired,
                requiredAttribute = attributes.FirstOrDefault(a => a is RequiredAttribute) as RequiredAttribute
            };

            if (memberInfo?.GetCustomAttribute<DescriptionAttribute>() is DescriptionAttribute descAttr)
            {
                if (!string.IsNullOrEmpty(descAttr.Description))
                    arg.Name = descAttr.Description;
            }

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
        /// <param name="fieldName"></param>
        /// <param name="validationErrors"></param>
        /// <exception cref="EntityGraphQLCompilerException"></exception>
        public void Validate(object? val, string fieldName, IList<string> validationErrors)
        {
            if (requiredAttribute != null && !requiredAttribute.IsValid(val))
                validationErrors.Add(requiredAttribute.ErrorMessage != null ? $"Field '{fieldName}' - {requiredAttribute.ErrorMessage}" : $"Field '{fieldName}' - missing required argument '{Name}'");
            else if (IsRequired && val == null && DefaultValue == null)
                validationErrors.Add($"Field '{fieldName}' - missing required argument '{Name}'");

            Type.SchemaType.Validate(val);
        }
    }
}