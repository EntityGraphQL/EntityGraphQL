using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace EntityGraphQL.Extensions;

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        FieldInfo fieldInfo = value.GetType().GetField(value.ToString())!;

        if (fieldInfo == null)
        {
            return value.ToString(); // Fallback to enum name if field not found
        }

        DescriptionAttribute[] attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);

        if (attributes != null && attributes.Length != 0)
        {
            return attributes.First().Description;
        }

        return value.ToString(); // Fallback to enum name if no DescriptionAttribute
    }
}
