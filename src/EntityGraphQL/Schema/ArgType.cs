using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using EntityGraphQL.Extensions;

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
        private RangeAttribute? rangeAttribute;
        private StringLengthAttribute? stringLengthAttribute;

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

        public static ArgType FromProperty(ISchemaProvider schema, PropertyInfo prop, object? defaultValue, Func<string, string> fieldNamer)
        {
            var arg = MakeArgType(schema, prop, prop.PropertyType, prop, defaultValue, fieldNamer);

            return arg;
        }

        public static ArgType FromParameter(ISchemaProvider schema, ParameterInfo prop, object? defaultValue, Func<string, string> fieldNamer)
        {
            var arg = MakeArgType(schema, prop.Member, prop.ParameterType, prop.Member, defaultValue, fieldNamer);

            return arg;
        }

        public static ArgType FromField(ISchemaProvider schema, FieldInfo field, object? defaultValue, Func<string, string> fieldNamer)
        {
            var arg = MakeArgType(schema, field, field.FieldType, field, defaultValue, fieldNamer);

            return arg;
        }

        private static ArgType MakeArgType(ISchemaProvider schema, MemberInfo memberInfo, Type type, MemberInfo field, object? defaultValue, Func<string, string> fieldNamer)
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

            var arg = new ArgType(fieldNamer(field.Name), field.Name, new GqlTypeInfo(() => schema.GetSchemaType(typeToUse.IsConstructedGenericType && typeToUse.GetGenericTypeDefinition() == typeof(EntityQueryType<>) ? typeof(string) : typeToUse.GetNonNullableOrEnumerableType(), null), typeToUse, memberInfo), memberInfo, type)
            {
                DefaultValue = defaultValue,
                IsRequired = markedRequired
            };

            arg.rangeAttribute = field.GetCustomAttribute(typeof(RangeAttribute), false) as RangeAttribute;
            arg.stringLengthAttribute = field.GetCustomAttribute(typeof(StringLengthAttribute), false) as StringLengthAttribute;
            arg.requiredAttribute = field.GetCustomAttribute(typeof(RequiredAttribute), false) as RequiredAttribute;
            if (arg.requiredAttribute != null || GraphQLNotNullAttribute.IsMemberMarkedNotNull(field))
            {
                arg.IsRequired = true;
            }

            if (arg.IsRequired)
                arg.Type.TypeNotNullable = true;
            if (arg.Type.TypeNotNullable)
                arg.IsRequired = true;

            if (field.GetCustomAttribute(typeof(DescriptionAttribute), false) is DescriptionAttribute d)
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

            if (rangeAttribute != null && !rangeAttribute.IsValid(val))
                validationErrors.Add(rangeAttribute.ErrorMessage != null ? $"Field '{fieldName}' - {rangeAttribute.ErrorMessage}" : $"Field '{fieldName}' - failed the range validation.");

            if (stringLengthAttribute != null && !stringLengthAttribute.IsValid(val))
                validationErrors.Add(stringLengthAttribute.ErrorMessage != null ? $"Field '{fieldName}' - {stringLengthAttribute.ErrorMessage}" : $"Field '{fieldName}' - failed the string length validation.");
        }
    }
}