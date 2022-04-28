using System;

namespace EntityGraphQL.Schema;

public interface ICustomTypeConverter
{
    Type Type { get; }

    /// <summary>
    /// Change a non-null value from fromType to toType using you're custom method
    /// </summary>
    /// <param name="value"></param>
    /// <param name="fromType"></param>
    /// <param name="toType"></param>
    /// <param name="schema"></param>
    /// <returns></returns>
    object? ChangeType(object value, Type toType, ISchemaProvider schema);
}