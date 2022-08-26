---
sidebar_position: 6
---

# Extension Attribute


[Field Extensions](../field-extensions/) and [Schema Directives](../directives/schema-directives) use the [ExtensionAttribute] or `IExtensionAttribute` interface to apply extensions to `IFields`and  `ISchemaTypes`. 

`ExtensionAttribute` is useful when building new Attributes (see `GraphQLOneOfAttribute`) whereas `IExtensionAttribute` allows you to use existing attributes (see `ObsoleteAttributeRegistration` which marks items with the [ObsoleteAttribute] as @deprecated).

```
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
```