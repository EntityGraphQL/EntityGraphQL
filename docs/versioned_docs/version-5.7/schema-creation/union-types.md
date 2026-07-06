---
sidebar_position: 7
---

# Union Types

[Union Types](https://graphql.org/learn/schema/#union-types) are very similar to interfaces, but they don't get to specify any common fields between the types.

Any abstract class or interface automatically added by the SchemaBuilder that contains no properties is added as a union instead of an interface (interfaces require at least one field).

You can register union types manually using the `AddUnion` method on SchemaProvider, then register potential types on the union type using the `SchemaField.AddPossibleType` method. This differs from interfaces in that you register the child classes on the parent instead of the parent on the children.

As C# does not support anything like union types they are implemented used blank 'marker interfaces'

```cs
public interface ICharacter { }
public class Human : ICharacter {
    ...
}
public class Droid : ICharacter {
    ...
}
// creating our schema
var union = schema.AddUnion<ICharacter>(name: "Character", description: "represents any character in the Star Wars trilogy");

schema.Type<ICharacter>().AddPossibleType<Human>();
schema.Type<ICharacter>().AddPossibleType<Droid>();
```
