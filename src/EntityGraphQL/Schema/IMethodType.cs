using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace EntityGraphQL.Schema
{
    public interface IMethodType
    {
        IDictionary<string, ArgType> Arguments { get; }
        string Name { get; }
        Type ReturnTypeClr { get; }
        string Description { get; }
        /// <summary>
        /// Get the GQL return type of the field or call
        /// </summary>
        /// <param name="schema"></param>
        /// <returns></returns>
        string GetReturnType(ISchemaProvider schema);
        bool ReturnTypeNotNullable { get; }
        bool ReturnElementTypeNullable { get; }
        RequiredClaims AuthorizeClaims { get; }

        ArgType GetArgumentType(string argName);
        bool HasArgumentByName(string argName);
    }

    public class ArgType
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Type Type { get; set; }
        public bool TypeNotNullable { get; set; }

        public static ArgType FromProperty(PropertyInfo prop)
        {
            var arg = new ArgType
            {
                Type = prop.PropertyType,
                Name = prop.Name,
                TypeNotNullable = GraphQLNotNullAttribute.IsMemberMarkedNotNull(prop) || prop.PropertyType.GetTypeInfo().IsEnum
            };

            if (prop.GetCustomAttribute(typeof(DescriptionAttribute), false) is DescriptionAttribute d)
            {
                arg.Description = d.Description;
            }

            return arg;
        }

        public static ArgType FromField(FieldInfo field)
        {
            var arg = new ArgType
            {
                Type = field.FieldType,
                Name = field.Name,
                TypeNotNullable = GraphQLNotNullAttribute.IsMemberMarkedNotNull(field) || field.FieldType.GetTypeInfo().IsEnum
            };

            if (field.GetCustomAttribute(typeof(DescriptionAttribute), false) is DescriptionAttribute d)
            {
                arg.Description = d.Description;
            }

            return arg;
        }
    }
}