using System;

namespace EntityGraphQL.Schema
{
    public abstract class ExtensionAttribute : Attribute
    {
        public virtual void ApplyExtension(IField field) { }

        public virtual void ApplyExtension(ISchemaType type) { }
    }

    public interface IExtensionAttribute<TAttribute> where TAttribute : Attribute
    {
        public void ApplyExtension(IField field, TAttribute attribute) { }
        public void ApplyExtension(ISchemaType type, TAttribute attribute) { }
    }
}