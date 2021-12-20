using System;

namespace EntityGraphQL.Schema
{
    internal interface ITypeSerializer
    {
        object Serialize(object value);
        object Deserialize(object value);
    }

    internal class TypeSerializer<TTypeDotNet, TTypeGql> : ITypeSerializer
    {
        private readonly Func<TTypeDotNet, TTypeGql> serialize;
        private readonly Func<TTypeGql, TTypeDotNet> deserialize;

        public TypeSerializer(Func<TTypeDotNet, TTypeGql> serialize, Func<TTypeGql, TTypeDotNet> deserialize)
        {
            this.serialize = serialize;
            this.deserialize = deserialize;
        }

        public object Deserialize(object value)
        {
            return deserialize((TTypeGql)value);
        }

        public object Serialize(object value)
        {
            return serialize((TTypeDotNet)value);
        }
    }
}