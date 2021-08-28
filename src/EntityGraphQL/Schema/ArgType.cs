using System;
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
        public string Name { get; set; }
        public string Description { get; set; }
        public GqlTypeInfo Type { get; set; }
        public object DefaultValue { get; set; }
        public MemberInfo MemberInfo { get; internal set; }
        public bool IsRequired { get; set; }
        public Type RawType { get; private set; }

        public static ArgType FromProperty(ISchemaProvider schema, PropertyInfo prop, object defaultValue)
        {
            var arg = MakeArgType(schema, prop, prop.PropertyType, prop, defaultValue);

            return arg;
        }

        public static ArgType FromField(ISchemaProvider schema, FieldInfo field, object defaultValue)
        {
            var arg = MakeArgType(schema, field, field.FieldType, field, defaultValue);

            return arg;
        }

        private static ArgType MakeArgType(ISchemaProvider schema, MemberInfo memberInfo, Type type, MemberInfo field, object defaultValue)
        {
            var markedRequired = false;
            var typeToUse = type;
            if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(RequiredField<>))
            {
                markedRequired = true;
                typeToUse = type.GetGenericArguments()[0];
            }

            var arg = new ArgType
            {
                Type = new GqlTypeInfo(() => schema.Type(typeToUse.IsConstructedGenericType && typeToUse.GetGenericTypeDefinition() == typeof(EntityQueryType<>) ? typeof(string) : typeToUse.GetNonNullableOrEnumerableType()), typeToUse),
                Name = field.Name,
                DefaultValue = defaultValue,
                MemberInfo = memberInfo,
                RawType = type,
                IsRequired = type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(RequiredField<>)
            };

            arg.Type.TypeNotNullable = GraphQLNotNullAttribute.IsMemberMarkedNotNull(field)
                || arg.Type.TypeNotNullable
                || field.GetCustomAttribute(typeof(RequiredAttribute), false) != null;
            if (markedRequired)
                arg.Type.TypeNotNullable = true;

            if (field.GetCustomAttribute(typeof(DescriptionAttribute), false) is DescriptionAttribute d)
            {
                arg.Description = d.Description;
            }

            return arg;
        }
    }
}