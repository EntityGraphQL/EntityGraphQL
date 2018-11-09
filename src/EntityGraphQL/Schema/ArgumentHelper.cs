using System;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Use this to mark arguments as required when building schemas with arguments.
    /// <code>schemaProvider.AddField("user", new {id = Required<int>()}, (ctx, param) => ctx.Users.Where(u => u.Id == param.id)</code>
    /// </summary>
    public static class ArgumentHelper
    {
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
        public TType Value { get; set; }

        public RequiredField()
        {
            Type = typeof(TType);
            Value = default(TType);
        }

        public RequiredField(TType value)
        {
            Type = typeof(TType);
            Value = value;
        }

        public static implicit operator TType(RequiredField<TType> field)
        {
            return field.Value;
        }

        public static implicit operator RequiredField<TType>(TType value)
        {
            return new RequiredField<TType>(value);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}