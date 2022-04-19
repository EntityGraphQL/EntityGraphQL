using System;

namespace EntityGraphQL.Schema;

public interface ICustomTypeConverter
{
    Type Type { get; }

    object? ChangeType(object value, Type fromType, Type toType, ISchemaProvider? schema);
}