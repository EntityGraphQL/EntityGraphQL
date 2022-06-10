---
title: 'Other Types'
metaTitle: 'Advanced types - EntityGraphQL'
metaDescription: 'Advanced types in EntityGraphQL'
---

So far we've only been dealing with GraphQL Object types. Types that are part of the object graph and have fields. These are the most common type in our schema, but lets look at the other types we can use.

# Scalar Types

We learnt previous that the [GraphQL spec](https://graphql.org/learn/schema/#scalar-types) defines the following built in scalar types.

- Int: A signed 32‐bit integer.
- Float: A signed double-precision floating-point value.
- String: A UTF‐8 character sequence.
- Boolean: true or false.
- ID: The ID scalar type represents a unique identifier. The ID type is serialized in the same way as a String; however, defining it as an ID signifies that it is not intended to be human‐readable.

We of course can add our own. Scalar types help you describe the data in you schema. Unlike Object types they don't have fields you can query, they result in data. Ultimately you are likely serializing the data to JSON for transport.

Adding a scalar type tells EntityGraphQL that the object should just be returned. i.e. there is no selection available on it. A good example is `DateTime`. We just want to return the `DateTime` value. Not have it as an Object type where you could select certain properties from it (Although you could set that up).

```
schema.AddScalarType<DateTime>("DateTime", "Represents a date and time.");
```

It also adds the type as a scalar type in the schema. You can also tell EntityGraphQL to auto-map a type to a schema type with `AddTypeMapping<TFromType>(string gqlType)`. For example

```
schema.AddTypeMapping<short>("Int");
```

By default EntityGraphQL maps these types to GraphQL types.

```
sbyte   ->  Int
short   ->  Int
ushort  ->  Int
long    ->  Int
ulong   ->  Int
byte    ->  Int
int     ->  Int
uint    ->  Int
float   ->  Float
double  ->  Float
decimal ->  Float
byte[]  ->  String
bool    ->  Boolean
```

# Enum Types

[Enum types](https://graphql.org/learn/schema/#enumeration-types) are just like you'd expect. It let's API consumers know that a field can be only 1 of a set of values.

With our `Person` example we could add a `Gender` enum.

```
[JsonConverter(typeof(StringEnumConverter))]
public enum Gender {
    Female,
    Male,
    NotSpecified
}

// building our schema
schema.AddEnum("Gender", typeof(Gender), "A persons Gender");
```

The GraphQL schema produced from this helps document and describe the data model to API users. Example GraphQL schema below

```
enum Gender {
	Female
	Male
    NotSpecified
}

type Person {
    firstName: String
    lastName: String
    gender: Gender
}
```

# Interfaces and Implements keyword

GraphQL supports [Interfaces](https://graphql.org/learn/schema/#interfaces) allowing you to define a abstract base type that multiple other types might implement.

EntityGraphQL automatically marks abstract classes and interfaces as GraphQL interfaces, however you can also add them manually to a schema with the AddInterface method on the SchemaProvider class.

```
public abstract class Character {
    public int Id { get; set; }
    public string Name { get; set; }
    public IEnumerable<Character> Friends { get; set; }
    public IEnumerable<Episode> AppearsIn { get; set; }
}

public class Human : Droid {
  public IEnumerable<Starship> starships { get; set; }
  public int TotalCredits { get; set;}
}

public class Droid : Character {
    public stirng PrimaryFunction { get; set;}
}

// creating our schema
schema.AddInterface<Character>(name: "Character", description: "represents any character in the Star Wars trilogy");
    .AddAllFields();

schema.AddInheritedType<Human>(name: "Human", "", baseType: "Character")
    .AddAllFields();

schema.AddInheritedType<Droid>(name: "Droid", "", baseType: "Character");
    .AddAllFields();
```

produces the graphql schema:

```
interface Character {
  id: ID!
  name: String!
  friends: [Character]
  appearsIn: [Episode]!
}

type Human implements Character {
  id: ID!
  name: String!
  friends: [Character]
  appearsIn: [Episode]!
  starships: [Starship]
  totalCredits: Int
}

type Droid implements Character {
  id: ID!
  name: String!
  friends: [Character]
  appearsIn: [Episode]!
  primaryFunction: String
}
```

# Input Types

We've seen passing scalar values, like enums, numbers or strings, as arguments into a field. [Input types](https://graphql.org/learn/schema/#input-types) allow us to define complex types that can be used as an argument. This is particularly valuable in the case of mutations, where you might want to pass in a whole object to be created.

Input types differ to regular Object types largely because they can't have arguments on their fields. There are just a data object. As described in the spec

> The fields on an input object type can themselves refer to input object types, but you can't mix input and output types in your schema. Input object types also can't have arguments on their fields.

```
public class PeopleMutations
{
    [GraphQLMutation("Add a new person to the system)]
    public Expression<Func<DemoContext, Person>> AddNewPerson(DemoContext db, AddPersonArgs args)
    {
        var person = new Person
        {
            FirstName = args.PersonInput.FirstName,
            LastName = args.PersonInput.LastName,
        };
        db.People.Add(person);
        db.SaveChanges();

        return (ctx) => ctx.People.First(p => p.Id == person.Id);
    }
}

[MutationArguments]
public class AddPersonArgs
{
    public PersonInput PersonInput { get; set; }
}

public class PersonInput
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}


// creating our schema
schema.AddInputType<PersonInput>("PersonInput", "New person data")
    .AddAllFields();
```

You could of course use the `Person` class from your data model directly and be selective about the fields you add to the GraphQL schema.

```
public class PeopleMutations
{
    [GraphQLMutation("Add a new person to the system)]
    public Expression<Func<DemoContext, Person>> AddNewPerson(DemoContext db, AddPersonArgs args)
    {
        db.People.Add(args.PersonInput);
        db.SaveChanges();
        return (ctx) => ctx.People.First(p => p.Id == person.Id);
    }
}

[MutationArguments]
public class AddPersonArgs
{
    public Person PersonInput { get; set; }
}

// creating our schema
var type = schema.AddInputType<Person>("PersonInput", "New person data")
type.AddField("firstName", p => p.FirstName, "First name);
type.AddField("lastName", p => p.LastName, "Last name);
```

You can also use complex types in field arguments.

```
public class FilterInput
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

schema.AddField(
    "people",
    new {
        filter = (FilterInput)null
    },
    (ctx, args) => ctx.People
        .WhereWhen(p => p.FistName == args.filter.firstName, !string.IsNullOrEmpty(args.filter.firstName))
        .WhereWhen(p => p.LastName == args.filter.lastName, !string.IsNullOrEmpty(args.filter.lastName)),
    "List of people optionally filtered by a first and/or last name"
);

schema.AddInputType<FilterInput>("FilterInput", "Filter data for people")
    .AddAllFields();
```

The larger impact of these choices can be seen in the resulting schema and use of the API.

With scalar arguments.

```
POST localhost:5000/graphql
    {
    "query": "mutation AddPerson($firstName: String!, $lastName: String!) {
        addNewPerson(firstName: $firstName, lastName: $lastName) { id }
    }",
    "variables": {
        "firstName": "Bill",
        "lastName": "Murray"
    }
}
```

With an input type argument

```
POST localhost:5000/graphql
{
    "query": "mutation AddPerson($person: PersonInput!) {
        addNewPerson(personInput: $person) { id }
    }",
    "variables": {
        "personInput": {
            "firstName": "Bill",
            "lastName": "Murray"
        }
    }
}
```

# Lists and Non-Null

GraphQL defines [type modifiers](https://graphql.org/learn/schema/#lists-and-non-null) specifically for declaring that a is a list or can not be `null`. In a schema these are `[T]` and `!`. For example a GraphQL schema might have the following.

```
enum Gender {
	Female
	Male
    NotSpecified
}

type Person {
    firstName: String!
    lastName: String!
    gender: Gender!
    friends: [Person]
}
```

The `!` on all the fields tells API users that those fields will not be `null`. And the `[]` around the `Person` type on the `friends` field types the API users that the field returns a list of `Person` objects.

EntityGraphQL will automatically figure out fields that return lists based on if the resolve expression returns an array or `IEnumerable<T>`.

Similarly EntityGraphQL will mark non-nullable .NET types as non-null in thr GraphQL schema. If you need to change that you can use `field.IsNullable(bool)`.

Lets say we know a person's first and last name will never be null.

```
var type = schema.AddInputType<Person>("PersonInput", "New person data")
type.AddField("firstName", p => p.FirstName, "First name).IsNullable(false);
type.AddField("lastName", p => p.LastName, "Last name).IsNullable(false);
```
