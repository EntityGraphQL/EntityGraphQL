using System;

namespace EntityGraphQL
{
    public static class ArgumentHelper
    {
        public static RequiredField<TType> Required<TType>()
        {
            return new RequiredField<TType>();
        }
    }

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