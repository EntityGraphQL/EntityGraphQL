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
        public string Name { get; private set; }
        public string DotnetName { get; private set; }
        public string Description { get; set; }
        public GqlTypeInfo Type { get; private set; }
        public object? DefaultValue { get; set; }
        public MemberInfo MemberInfo { get; internal set; }
        public bool IsRequired { get; set; }
        public Type RawType { get; private set; }

        public ArgType(string name, string dotnetName, GqlTypeInfo type, MemberInfo memberInfo, Type rawType)
        {
            Name = name;
            DotnetName = dotnetName;
            Description = "";
            Type = type;
            MemberInfo = memberInfo;
            RawType = rawType;
        }

        public static ArgType FromProperty(ISchemaProvider schema, PropertyInfo prop, object? defaultValue, Func<string, string> fieldNamer)
        {
            var arg = MakeArgType(schema, prop, prop.PropertyType, prop, defaultValue, fieldNamer);

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
            else if (field.GetCustomAttribute(typeof(RequiredAttribute), false) != null
                || GraphQLNotNullAttribute.IsMemberMarkedNotNull(field))
            {
                markedRequired = true;
                defaultValue = null;
            }

            var arg = new ArgType(fieldNamer(field.Name), field.Name, new GqlTypeInfo(() => schema.GetSchemaType(typeToUse.IsConstructedGenericType && typeToUse.GetGenericTypeDefinition() == typeof(EntityQueryType<>) ? typeof(string) : typeToUse.GetNonNullableOrEnumerableType(), null), typeToUse), memberInfo, type)
            {
                DefaultValue = defaultValue,
                IsRequired = markedRequired
            };

            if (markedRequired)
                arg.Type.TypeNotNullable = true;
            if (arg.Type.TypeNotNullable)
                arg.IsRequired = true;

            if (field.GetCustomAttribute(typeof(DescriptionAttribute), false) is DescriptionAttribute d)
            {
                arg.Description = d.Description;
            }

            return arg;
        }
    }
}