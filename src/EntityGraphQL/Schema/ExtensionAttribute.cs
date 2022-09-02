using System;
using System.Collections.Generic;

namespace EntityGraphQL.Schema
{
    public abstract class ExtensionAttribute : Attribute
    {
        public virtual void ApplyExtension(IField field) { }

        public virtual void ApplyExtension(ISchemaType type) { }
    }

    /// <summary>
    /// Used to handle other Attributes found in the schema where you can not extend from ExtensionAttribute
    /// </summary>
    /// <typeparam name="TAttribute"></typeparam>
    public abstract class AbstractExtensionAttributeHandler<TAttribute> : IExtensionAttributeHandler where TAttribute : Attribute
    {
        public IEnumerable<Type> AttributeTypes { get => new List<Type> { typeof(TAttribute) }; }
        public virtual void ApplyExtension(IField field, Attribute attribute)
        {
            if (attribute is TAttribute tAttribute)
                ApplyExtension(field, tAttribute);
            else
                throw new ArgumentException($"Attribute must be of type {typeof(TAttribute).Name}");
        }
        public virtual void ApplyExtension(ISchemaType type, Attribute attribute)
        {
            if (attribute is TAttribute tAttribute)
                ApplyExtension(type, tAttribute);
            else
                throw new ArgumentException($"Attribute must be of type {typeof(TAttribute).Name}");
        }

        public virtual void ApplyExtension(IField field, TAttribute attribute) { }
        public virtual void ApplyExtension(ISchemaType type, TAttribute attribute) { }
    }
    public interface IExtensionAttributeHandler
    {
        /// <summary>
        /// List of Attribute types this handler can handle
        /// </summary>
        IEnumerable<Type> AttributeTypes { get; }

        public void ApplyExtension(IField field, Attribute attribute);
        public void ApplyExtension(ISchemaType type, Attribute attribute);
    }
}