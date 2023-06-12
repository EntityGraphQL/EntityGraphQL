using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

public class ValidationTests
{
    [Fact]
    public void TestValidationAttributesOnMutationArgs()
    {
        var schema = SchemaBuilder.FromObject<ValidationTestsContext>();
        schema.AddMutationsFrom<ValidationTestsMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = true });
        var gql = new QueryRequest
        {
            Query = @"mutation Mutate {
                addMovie(price: 150 rating: ""this is too long"", cast: [{ actor: """" character: ""Barney"" }]) {
                    id
                }
            }",
        };

        var testContext = new ValidationTestsContext();
        var results = schema.ExecuteRequestWithContext(gql, testContext, null, null);
        Assert.NotNull(results.Errors);
        Assert.Equal(7, results.Errors.Count);
        Assert.Equal("Field 'addMovie' - Title is required", results.Errors[0].Message);
        Assert.Equal("Field 'addMovie' - Date is required", results.Errors[1].Message);
        Assert.Equal("Field 'addMovie' - Genre must be specified", results.Errors[2].Message);
        Assert.Equal("Field 'addMovie' - Price must be between $1 and $100", results.Errors[3].Message);
        Assert.Equal("Field 'addMovie' - Rating must be less than 5 characters", results.Errors[4].Message);
        Assert.Equal("Field 'addMovie' - Actor is required", results.Errors[5].Message);
        Assert.Equal("Field 'addMovie' - Character must be less than 5 characters", results.Errors[6].Message);
    }

    [Fact]
    public void TestValidationAttributesOnNestedMutationArgs()
    {
        var schema = SchemaBuilder.FromObject<ValidationTestsContext>();
        schema.AddMutationsFrom<ValidationTestsMutations>(new SchemaBuilderOptions { AutoCreateInputTypes = true });
        var gql = new QueryRequest
        {
            Query = @"mutation Mutate {
                addMovie(title: ""movie name"" releaseDate: ""2020-01-01"" genre: ""Comedy"" price: 99 rating: ""short"", cast: [{ actor: """" character: ""Barney"" }]) {
                    id
                }
            }",
        };

        var testContext = new ValidationTestsContext();
        var results = schema.ExecuteRequestWithContext(gql, testContext, null, null);
        Assert.NotNull(results.Errors);
        Assert.Equal(2, results.Errors.Count);
        Assert.Equal("Field 'addMovie' - Actor is required", results.Errors[0].Message);
        Assert.Equal("Field 'addMovie' - Character must be less than 5 characters", results.Errors[1].Message);
    }

    [Fact]
    public void TestCustomValidationOnMutationArgs()
    {
        var schema = SchemaBuilder.FromObject<ValidationTestsContext>();
        schema.Mutation()
        .Add(AddPerson)
        .AddValidator<PersonValidator>();
        var gql = new QueryRequest
        {
            Query = @"mutation Mutate {
                addPerson(name: ""Luke"")
            }",
        };

        var testContext = new ValidationTestsContext();
        var results = schema.ExecuteRequestWithContext(gql, testContext, null, null);
        Assert.NotNull(results.Errors);
        Assert.Single(results.Errors);
        Assert.Equal("Field 'addPerson' - Name can't be Luke", results.Errors[0].Message);
    }

    [Fact]
    public void TestGraphQLValidatorWithInlineArgs()
    {
        var schema = SchemaBuilder.FromObject<ValidationTestsContext>();

        schema.AddMutationsFrom<ValidationTestsMutations>(new SchemaBuilderOptions() { AutoCreateInputTypes = true });

        var gql = new QueryRequest
        {
            Query = @"mutation Mutate($arg: CastMemberArg) {
                updateCastMemberWithGraphQLValidator(arg: $arg)
            }",
            Variables = new QueryVariables()
            {
                { "arg", new { Actor = "Neil", Character = "Barn" } }
            }
        };

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IGraphQLValidator, GraphQLValidator>();
        var testContext = new ValidationTestsContext();
        var results = schema.ExecuteRequestWithContext(gql, testContext, serviceCollection.BuildServiceProvider(), null);
        Assert.NotNull(results.Errors);
        Assert.Single(results.Errors);
        Assert.Equal("Test Error", results.Errors[0].Message);
    }

    [Fact]
    public void TestCustomValidationDelegateOnMutation()
    {
        var schema = SchemaBuilder.FromObject<ValidationTestsContext>();
        schema.Mutation()
        .Add(AddPerson)
        .AddValidator(context =>
        {
            if (context.Arguments is PersonArgs args)
            {
                if (args.Name == "Luke")
                    context.AddError("Name can't be Luke");
            }
        });
        var gql = new QueryRequest
        {
            Query = @"mutation Mutate {
                addPerson(name: ""Luke"")
            }",
        };

        var testContext = new ValidationTestsContext();
        var results = schema.ExecuteRequestWithContext(gql, testContext, null, null);
        Assert.NotNull(results.Errors);
        Assert.Single(results.Errors);
        Assert.Equal("Field 'addPerson' - Name can't be Luke", results.Errors[0].Message);
    }

    [Fact]
    public void TestCustomValidationAttributeOnMutationMethod()
    {
        var schema = SchemaBuilder.FromObject<ValidationTestsContext>();
        schema.Mutation().AddFrom<ValidationTestsMutations>();
        var gql = new QueryRequest
        {
            Query = @"mutation Mutate {
                addPersonValidatorAttribute(name: ""Luke"")
            }",
        };

        var testContext = new ValidationTestsContext();
        var results = schema.ExecuteRequestWithContext(gql, testContext, null, null);
        Assert.NotNull(results.Errors);
        Assert.Single(results.Errors);
        Assert.Equal("Field 'addPersonValidatorAttribute' - Name can't be Luke", results.Errors[0].Message);
    }

    [Fact]
    public void TestCustomValidationAttributeOnMutationMethodArgType()
    {
        var schema = SchemaBuilder.FromObject<ValidationTestsContext>();
        schema.Mutation().Add("addPersonValidatorOnArgs", (PersonArgsWithValidator args) =>
        {
            return true;
        });
        var gql = new QueryRequest
        {
            Query = @"mutation Mutate {
                addPersonValidatorOnArgs(name: ""Luke"")
            }",
        };

        var testContext = new ValidationTestsContext();
        var results = schema.ExecuteRequestWithContext(gql, testContext, null, null);
        Assert.NotNull(results.Errors);
        Assert.Single(results.Errors);
        Assert.Equal("Field 'addPersonValidatorOnArgs' - Name can't be Luke", results.Errors[0].Message);
    }

    [Fact]
    public void TestValidationAttributesOnQueryFieldArgs()
    {
        var schema = SchemaBuilder.FromObject<ValidationTestsContext>();
        schema.Query().ReplaceField("movies", new MovieQueryArgsWithAttributes(), (ctx, args) => ctx.Movies.Where(m => m.Title.Contains(args.Title)), "Movies");
        var gql = new QueryRequest
        {
            Query = @"query {
                movies(price: 150 title: ""this is too long"") {
                    id
                }
            }",
        };

        var testContext = new ValidationTestsContext();
        var results = schema.ExecuteRequestWithContext(gql, testContext, null, null);
        Assert.NotNull(results.Errors);
        Assert.Equal(3, results.Errors.Count);
        Assert.Equal("Field 'movies' - Genre is required", results.Errors[0].Message);
        Assert.Equal("Field 'movies' - Title must be less than 5 characters", results.Errors[1].Message);
        Assert.Equal("Field 'movies' - Price must be between $1 and $100", results.Errors[2].Message);
    }

    [Fact]
    public void TestCustomValidationValidatorOnQueryFieldArgs()
    {
        var schema = SchemaBuilder.FromObject<ValidationTestsContext>();
        schema.Query().ReplaceField("movies",
            new MovieQueryArgs(), (ctx, args) => ctx.Movies.Where(m => m.Title.Contains(args.Title)),
            "Get a list of Movies")
            .AddValidator<MovieValidator>();
        var gql = new QueryRequest
        {
            Query = @"query {
                movies(price: 150) {
                    id
                }
            }",
        };

        var testContext = new ValidationTestsContext();
        var results = schema.ExecuteRequestWithContext(gql, testContext, null, null);
        Assert.NotNull(results.Errors);
        Assert.Equal(2, results.Errors.Count);
        Assert.Equal("Field 'movies' - You can't use 150 for the price", results.Errors[0].Message);
        Assert.Equal("Field 'movies' - Empty or null Title is an invalid search term", results.Errors[1].Message);
    }

    [Fact]
    public void TestCustomValidationValidatorOnQueryFieldAnonArgs()
    {
        var schema = SchemaBuilder.FromObject<ValidationTestsContext>();
        schema.Query().ReplaceField("movies",
            // use anonymous type args
            new
            {
                search = (string)null
            },
            (ctx, args) => ctx.Movies.Where(m => m.Title.Contains(args.search)),
            "Movies")
            .AddValidator<MovieValidatorAnon>();
        var gql = new QueryRequest
        {
            Query = @"query {
                movies {
                    id
                }
            }",
        };

        var testContext = new ValidationTestsContext();
        var results = schema.ExecuteRequestWithContext(gql, testContext, null, null);
        Assert.NotNull(results.Errors);
        Assert.Single(results.Errors);
        Assert.Equal("Field 'movies' - search arg cannot be empty or null", results.Errors[0].Message);
    }

    [Fact]
    public void TestCustomValidationValidatorDelegateOnQueryFieldArgs()
    {
        var schema = SchemaBuilder.FromObject<ValidationTestsContext>();
        schema.Query().ReplaceField("movies",
            new MovieQueryArgs(), (ctx, args) => ctx.Movies.Where(m => m.Title.Contains(args.Title)),
            "Get a list of Movies")
            .AddValidator((context) =>
            {
                if (context.Arguments is MovieQueryArgs args)
                {
                    if (args.Price == 150)
                        context.AddError("You can't use 150 for the price");
                    if (string.IsNullOrEmpty(args.Title))
                        context.AddError("Empty or null Title is an invalid search term");
                }
            });
        var gql = new QueryRequest
        {
            Query = @"query {
                movies(price: 150) {
                    id
                }
            }",
        };

        var testContext = new ValidationTestsContext();
        var results = schema.ExecuteRequestWithContext(gql, testContext, null, null);
        Assert.NotNull(results.Errors);
        Assert.Equal(2, results.Errors.Count);
        Assert.Equal("Field 'movies' - You can't use 150 for the price", results.Errors[0].Message);
        Assert.Equal("Field 'movies' - Empty or null Title is an invalid search term", results.Errors[1].Message);
    }
    [Fact]
    public void TestCustomValidationValidatorDelegateAsyncOnQueryFieldArgs()
    {
        var schema = SchemaBuilder.FromObject<ValidationTestsContext>();
        schema.Query().ReplaceField("movies",
            new MovieQueryArgs(), (ctx, args) => ctx.Movies.Where(m => m.Title.Contains(args.Title)),
            "Get a list of Movies")
            .AddValidator(async context =>
            {
                // pretend await
                await System.Threading.Tasks.Task.Run(() =>
                {
                    if (context.Arguments is MovieQueryArgs args)
                    {
                        if (args.Price == 150)
                            context.AddError("You can't use 150 for the price");
                        if (string.IsNullOrEmpty(args.Title))
                            context.AddError("Empty or null Title is an invalid search term");
                    }
                });
            });
        var gql = new QueryRequest
        {
            Query = @"query {
                movies(price: 150) {
                    id
                }
            }",
        };

        var testContext = new ValidationTestsContext();
        var results = schema.ExecuteRequestWithContext(gql, testContext, null, null);
        Assert.NotNull(results.Errors);
        Assert.Equal(2, results.Errors.Count);
        Assert.Equal("Field 'movies' - You can't use 150 for the price", results.Errors[0].Message);
        Assert.Equal("Field 'movies' - Empty or null Title is an invalid search term", results.Errors[1].Message);
    }

    [Fact]
    public void TestCustomValidationAttributeOnQueryFieldArgs()
    {
        var schema = SchemaBuilder.FromObject<ValidationTestsContext>();
        schema.Query().ReplaceField("movies", new MovieQueryArgsWithValidator(), (ctx, args) => ctx.Movies.Where(m => m.Title.Contains(args.Title)), "Movies");
        var gql = new QueryRequest
        {
            Query = @"query {
                movies(title: """") {
                    id
                }
            }",
        };

        var testContext = new ValidationTestsContext();
        var results = schema.ExecuteRequestWithContext(gql, testContext, null, null);
        Assert.NotNull(results.Errors);
        Assert.Equal(2, results.Errors.Count);
        Assert.Equal("Field 'movies' - Genre is required", results.Errors[0].Message);
        Assert.Equal("Field 'movies' - Empty or null Title is an invalid search term", results.Errors[1].Message);
    }

    private static bool AddPerson(PersonArgs args)
    {
        return true;
    }
}

internal class PersonValidator : IArgumentValidator
{
    public System.Threading.Tasks.Task ValidateAsync(ArgumentValidatorContext context)
    {
        if (context.Arguments is PersonArgs args)
        {
            if (args.Name == "Luke")
                context.AddError("Name can't be Luke");
        }
        else if (context.Arguments is PersonArgsWithValidator args2)
        {
            if (args2.Name == "Luke")
                context.AddError("Name can't be Luke");
        }
        return System.Threading.Tasks.Task.CompletedTask;
    }
}

internal class MovieValidator : IArgumentValidator
{
    public System.Threading.Tasks.Task ValidateAsync(ArgumentValidatorContext context)
    {
        if (context.Arguments is MovieQueryArgs args)
        {
            if (args.Price == 150)
                context.AddError("You can't use 150 for the price");
            if (string.IsNullOrEmpty(args.Title))
                context.AddError("Empty or null Title is an invalid search term");
        }
        else if (context.Arguments is MovieQueryArgsWithValidator args2)
        {
            if (string.IsNullOrEmpty(args2.Genre))
                context.AddError("Genre is required");
            if (string.IsNullOrEmpty(args2.Title))
                context.AddError("Empty or null Title is an invalid search term");
        }
        return System.Threading.Tasks.Task.CompletedTask;
    }
}

internal class MovieValidatorAnon : IArgumentValidator
{
    public System.Threading.Tasks.Task ValidateAsync(ArgumentValidatorContext context)
    {
        dynamic args = context.Arguments;
        if (string.IsNullOrEmpty(args.search))
            context.AddError("search arg cannot be empty or null");
        return System.Threading.Tasks.Task.CompletedTask;
    }
}

internal class MovieQueryArgs
{
    public string Title { get; set; }
    public decimal Price { get; set; }
    public string Genre { get; set; }
}

internal class MovieQueryArgsWithAttributes
{
    [StringLength(5, ErrorMessage = "Title must be less than 5 characters")]
    public string Title { get; set; }
    [Range(1, 100, ErrorMessage = "Price must be between $1 and $100")]
    public decimal Price { get; set; }
    [Required(ErrorMessage = "Genre is required")]
    public string Genre { get; set; }
}
[ArgumentValidator(typeof(MovieValidator))]
internal class MovieQueryArgsWithValidator
{
    public string Title { get; set; }
    public string Genre { get; set; }
}

internal class ValidationTestsContext
{
    public DbSet<Movie> Movies { get; set; }
}

internal class ValidationTestsMutations
{
    [GraphQLMutation]
    public static Expression<Func<ValidationTestsContext, Movie>> AddMovie(MovieArg movie)
    {
        var newMovie = new Movie
        {
            Id = new Random().Next(),
            Title = movie.Title,
        };
        return c => c.Movies.SingleOrDefault(m => m.Id == newMovie.Id);
    }

    [GraphQLMutation]
    [ArgumentValidator(typeof(PersonValidator))]
    public static bool AddPersonValidatorAttribute(PersonArgs _)
    {
        return true;
    }


    [GraphQLMutation]
    public static bool UpdateCastMemberWithGraphQLValidator(CastMemberArg arg, IGraphQLValidator validator)
    {
        validator.AddError("Test Error");
        return true;
    }
}

[GraphQLArguments]
internal class MovieArg
{
    [Required(ErrorMessage = "Title is required")]
    public string Title { get; set; }

    [Required(ErrorMessage = "Date is required")]
    public DateTime ReleaseDate { get; set; }

    [Required(ErrorMessage = "Genre must be specified")]
    public string Genre { get; set; }

    [Range(1, 100, ErrorMessage = "Price must be between $1 and $100")]
    public decimal Price { get; set; }

    [StringLength(5, ErrorMessage = "Rating must be less than 5 characters")]
    public string Rating { get; set; }

    public IList<CastMemberArg> Cast { get; set; }
}

internal class CastMemberArg
{
    [Required(ErrorMessage = "Actor is required")]
    [StringLength(20, ErrorMessage = "Actor Name must be less than 20 characters")]
    public string Actor { get; set; }
    [StringLength(5, ErrorMessage = "Character must be less than 5 characters")]
    public string Character { get; set; }
}

[GraphQLArguments]
internal class PersonArgs
{
    public string Name { get; set; }
}

[GraphQLArguments]
[ArgumentValidator(typeof(PersonValidator))]
internal class PersonArgsWithValidator
{
    public string Name { get; set; }
}