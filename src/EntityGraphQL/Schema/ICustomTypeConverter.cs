using System;

namespace EntityGraphQL.Schema;

public interface ICustomTypeConverter
{
    Type Type { get; }

    /// <summary>
    /// Change a non-null value of fromType to toType using you're custom method
    /// </summary>
    /// <returns>The new object as toType</returns>
    object? ChangeType(object value, Type toType, ISchemaProvider schema);
}
