using System;

namespace EntityGraphQL.Schema;

/// <summary>
/// Use this to mark arguments as required when building schemas with arguments.
/// <code>schemaProvider.AddField("user", new {id = Required<int>()}, (ctx, param) => ctx.Users.Where(u => u.Id == param.id)</code>
/// </summary>
public static class ArgumentHelper
{
    /// <summary>
    /// Creates a required argument with the specified type.
    /// </summary>
    /// <typeparam name="TType"></typeparam>
    /// <returns></returns>
    public static RequiredField<TType> Required<TType>()
    {
        return new RequiredField<TType>();
    }
}

/// <summary>
/// Wraps a field/argument, marking it as required when building schemas
/// </summary>
/// <typeparam name="TType"></typeparam>
public class RequiredField<TType>
{
    public Type Type { get; }
    public TType? Value { get; set; }

    public RequiredField()
    {
        Type = typeof(TType);
        Value = default;
    }

    public RequiredField(TType value)
    {
        Type = typeof(TType);
        Value = value;
    }

    public static implicit operator TType(RequiredField<TType> field)
    {
        if (field.Value == null)
            throw new EntityGraphQLException(
                GraphQLErrorCategory.ExecutionError,
                $"Required field argument being used without a value being set. Are you trying to use RequiredField outside a of field expression?"
            );
        return field.Value;
    }

    public static implicit operator RequiredField<TType>(TType value)
    {
        return new RequiredField<TType>(value);
    }

    public override string ToString()
    {
        return Value?.ToString() ?? "null";
    }
}
