---
sidebar_position: 6
---

# Event System

A couple of events has been added to fields to support other functionality which could be used in other ways, there is an intention to build these events out further in the future.

## ISchemaType

* OnAddField - called before adding a field to a type, allows you to throw an exception if the field is invalid in some way
* OnValidate - called when validating an InputType before being passed to a mutation

The `@oneOf` [Schema Directive](../directives/schema-directives) uses both these events

```
public static class OneOfDirectiveExtensions
{
    public static void OneOf(this ISchemaType type)
    {
        type.AddDirective(new OneOfDirective());
        type.OnAddField += (field) => { 
            if (field.ReturnType.TypeNotNullable)
            {
                throw new EntityQuerySchemaException($"{type.TypeDotnet.Name} is a OneOf type but all its fields are not nullable. OneOf input types require all the field to be nullable.");
            }
        };
        type.OnValidate += (value) => {
            if (value != null)
            {
                var singleField = value.GetType().GetProperties().Count(x => x.GetValue(value) != null);
                
                if (singleField != 1) // we got multiple set
                    throw new EntityGraphQLValidationException($"Exactly one field must be specified for argument of type {type.Name}.");
            }
        };
    }
}
```