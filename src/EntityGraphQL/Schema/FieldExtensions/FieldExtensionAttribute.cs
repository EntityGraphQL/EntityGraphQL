using System;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public abstract class FieldExtensionAttribute : Attribute
    {
        public abstract void ApplyExtension(Field field);
    }
}