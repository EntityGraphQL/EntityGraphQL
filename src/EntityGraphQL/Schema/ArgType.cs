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

        private static ArgType MakeArgType(ISchemaProvider schema, string name, MemberInfo? memberInfo, IEnumerable<Attribute> attributes, Type type, object? defaultValue, NullabilityInfoEx nullability)
        {
            var markedRequired = false;
            var typeToUse = type;
            if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(RequiredField<>))
            {
                markedRequired = true;
                typeToUse = type.GetGenericArguments()[0];
                // default value will often be the default value of the non-null type (e.g. 0 for int). 
                // We are saying here it must be provided by the query
                defaultValue = null;
            }

            var gqlTypeInfo = new GqlTypeInfo(() => schema.GetSchemaType(typeToUse.IsConstructedGenericType && typeToUse.GetGenericTypeDefinition() == typeof(EntityQueryType<>) ? typeof(string) : typeToUse.GetNonNullableOrEnumerableType(), null), typeToUse, nullability);
            var arg = new ArgType(schema.SchemaFieldNamer(name), name, gqlTypeInfo, type)
            {
                DefaultValue = defaultValue,
                IsRequired = markedRequired,
                requiredAttribute = attributes.FirstOrDefault(a => a is RequiredAttribute) as RequiredAttribute
            };

            if (arg.requiredAttribute != null || GraphQLNotNullAttribute.IsMemberMarkedNotNull(attributes) || nullability.WriteState == NullabilityStateEx.NotNull)
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