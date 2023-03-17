---
sidebar_position: 6
---

# Extension Attribute

[Field Extensions](../field-extensions/) and [Schema Directives](../directives/schema-directives) use the [ExtensionAttribute] or `IExtensionAttributeHandler` interface to apply extensions to `IFields` and `ISchemaTypes`.

`ExtensionAttribute` is useful when building new Attributes (see `GraphQLOneOfAttribute`) whereas `IExtensionAttributeHandler` allows you to use existing attributes (see `ObsoleteAttributeHandler` which marks items with the [ObsoleteAttribute] as @deprecated).

```
public abstract class ExtensionAttribute : Attribute
{
    public virtual void ApplyExtension(IField field) { }
    public virtual void ApplyExtension(ISchemaType type) { }
}

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
    IEnumerable<Type> AttributeTypes { get; }

    public void ApplyExtension(IField field, Attribute attribute);
    public void ApplyExtension(ISchemaType type, Attribute attribute);
}
```
