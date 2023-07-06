---
sidebar_position: 6
---

# Validation

When you privide input - via arguments - to your queries, you may want to perform some validation on that input before proceeding to you domain logic. EntityGraphQL provides a couple of ways to validation your input.

## Mutation Validation

You can use the `[Required]`, `[Range]` & `[StringLength]` attributes to add validaiton to your mutation arguments. Example

```cs
public Expression<Func<DemoContext, Person>> AddNewPerson(DemoContext db, AddPersonArgs args)
{
    // Add person logic here
    return (ctx) => ctx.People.First(p => p.Id == person.Id);
}

[MutationArguments]
public class AddPersonArgs
{
  [Required(AllowEmptyStrings = false, ErrorMessage = "Actor Name is required")]
  public string Name { get; set; }
  [Range(0, 900, ErrorMessage = "Age must be positive and less than 900")]
  public int Age { get; set; }
  [StringLength(200, ErrorMessage = "Description must be less than 200 characters")]
  public string Description { get; set; }
}
```

If any of those validations fail, the graph QL result will have errors for each one that failed. If your model validation fails your mutation method _will not be called_.

Throwing an exception in your mutation will cause the the error to be reported in the GraphQL response. You can also collect multiple error messages instead of throwing an exception on the first error using the `GraphQLValidator` service.

This service needs to be registered in your service provider. You can always implement you own `GraphQLValidator` by implementing the `IGraphQLValidator` interface.

```cs

// In your Startup.cs
services.AddGraphQLValidator(); // with EntityGraphQL.AspNet

// Or without / or you own implementation
services.AddScoped<IGraphQLValidator, GraphQLValidator>();

// your mutation
public class MovieMutations
{
  [GraphQLMutation]
  public Expression<Func<MyDbContext, Movie>> AddActor(MyDbContext db, ActorArgs args, GraphQLValidator validator)
  {
    if (string.IsNullOrEmpty(args.Name))
      validator.AddError("Name argument is required");
    if (args.age <= 0)
      validator.AddError("Age argument must be positive");

    if (validator.HasErrors)
      return null;

    // do your magic here. e.g. with EF or other business logic
    var movie = db.Movies.First(m => m.Id == args.Id);
    var actor = new Person { Name = args.Name, ... };
    movie.Actors.Add(actor);
    db.SaveChanges();
    return ctx => ctx.Movies.First(m => m.Id == movie.Id);
  }
}
```

## Query Field Validation

If a query field has arguments - like an Id to search for or a filter string etc. - you may also want to perform validation on the input. You can use the same `[Required]`, `[Range]` & `[StringLength]` attributes on your argument object if you are using typed field arguments.

```cs
schema.Query().AddField(
    "people",
    new PeopleArgs(),
    (ctx, args) => ctx.People.Where(p => p.Age > args.Age),
    "List all people. Filter by min age"
);

public class PeopleArgs
{
    [Range(0, 115)]
    public bool Age { get; set; } = 0 // default value
}
```

## Custom Validation with Argument Validators

EntityGraphQL allows you to register argument validator which is a great place to perform custom validation on any arguments. If you add any errors to the `ArgumentValidatorContext` you are passed execution of the the query will not proceed and the errors will be in the result.

You can register validators via a class that implements `IArgumentValidator`.

```cs
schema.Query().AddField("movies",
    new MovieQueryArgs(), (ctx, args) => ctx.Movies.Where(m => m.Title.Contains(args.Title)),
    "Get a list of Movies")
    .AddValidator<MovieValidator>();

public class MovieValidator : IArgumentValidator
{
    public Task InvokeAsync(ArgumentValidatorContext context)
    {
        // should always be true
        if (context.Arguments is MovieQueryArgs args)
        {
            if (args.Price == 150)
                context.AddError("You can't use 150 for the price");
            if (string.IsNullOrEmpty(args.Title))
                context.AddError("Empty or null Title is an invalid search term");
        }
        return System.Threading.Tasks.Task.CompletedTask;
    }
}
```

Or as a deletgate.

```cs
schema.Query().AddField("movies",
    new MovieQueryArgs(), (ctx, args) => ctx.Movies.Where(m => m.Title.Contains(args.Title)),
    "Get a list of Movies")
    .AddValidator(context => {
        // should always be true
        if (context.Arguments is MovieQueryArgs args)
        {
            if (args.Price == 150)
                context.AddError("You can't use 150 for the price");
            if (string.IsNullOrEmpty(args.Title))
                context.AddError("Empty or null Title is an invalid search term");
        }
    });
```

If you are using anonymous type for arguments you can use the arguments as a `dynamic` type.

```cs
schema.Query().AddField("movies",
    new MovieQueryArgs(), (ctx, args) => ctx.Movies.Where(m => m.Title.Contains(args.Title)),
    "Get a list of Movies")
    .AddValidator(context => {
        dynamic args = context.Arguments;
        if (args.Price == 150)
            context.AddError("You can't use 150 for the price");
        if (string.IsNullOrEmpty(args.Title))
            context.AddError("Empty or null Title is an invalid search term");
    });
```

You can add validators to mutation fields. Validators on a mutation field are called before the mutation is executed, if you add any errors to the `ArgumentValidatorContext` the mutation will not be executed and the errors will be in the result.

```cs
[Fact]
var schema = SchemaBuilder.FromObject<ValidationTestsContext>();
schema.Mutation().Add(AddPerson)
.AddValidator(context =>
{
    if (context.Arguments is PersonArgs args)
    {
        if (args.Name == "Luke")
            context.AddError("Name can't be Luke");
    }
});

private static bool AddPerson(PersonArgs args)
{
    // ...
}

public class PersonArgs
{
    public string Name { get; set; }
}
```

## Validation with Attributes

You can use the `ArgumentValidator` attribute to register validator on input arguments as well. These can be used on

- Mutation methods - Will trigger validation for the arguments for the mutation
- Mutation arguments class - the arguments passed to a mutation
- Typed query arguments - if you have typed query arguments with this attribute they will be validated. If you use anonymous argument types you will need to use `AddValidator()`

```cs
// On mutation arguments directly
[MutationArguments]
[ArgumentValidator(typeof(PersonValidator))]
public class PersonArgs
{
    public string Name { get; set; }
}

// On the mutation method
public class Mutations
{
    [GraphQLMutation]
    [ArgumentValidator(typeof(PersonValidator))]
    public static bool AddPerson(PersonArgs args)
    {
        // ...
    }
}

// On query args
schema.Query().AddField("movies",
    new MovieQueryArgs(),
    (ctx, args) => ctx.Movies.Where(m => m.Title.Contains(args.Title)),
    "List of movies");

[ArgumentValidator(typeof(MovieValidator))]
public class MovieQueryArgs
{
    public string Title { get; set; }
}
```

## Extensions - adding custom data to errors

Errors often can be useful for end users. Other times they are for the developers. You can use the extensions field to add additional information to your errors to help you make a choice about the error. I.e. like showing it directly to the user. Extensions are defined as `Dictionary<string, object>`

```cs
public class MovieMutations
{
  [GraphQLMutation]
  public Expression<Func<MyDbContext, Movie>> AddActor(MyDbContext db, ActorArgs args, GraphQLValidator validator)
  {
    if (string.IsNullOrEmpty(args.Name))
      validator.AddError("Name argument is required", new Dictionary<string, object> {{"type", 1}});
    if (args.age <= 0)
      validator.AddError("Age argument must be positive", new Dictionary<string, object> {{"type", 1}});

    if (validator.HasErrors)
      return null;

    // do your magic here. e.g. with EF or other business logic
    //...
  }
}
```

The result will look like this

```json
{
  "errors": [
    {
      "message": "Name argument is required",
      "extensions": {
        "type": 1
      }
    }
  ]
}
```

You can they check the `type` field on the error and display it to the user.

You can also `throw` the `EntityGraphQLException` which also takes an extensions arguments.
