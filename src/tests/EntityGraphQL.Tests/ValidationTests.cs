using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EntityGraphQL.Tests;

public class ValidationTests
{
    [Fact]
    public void TestValidationAttributesOnMutationArgs()
    {
        var schema = SchemaBuilder.FromObject<ValidationTestsContext>();
        schema.AddMutationsFrom(new ValidationTestsMutations());
        var gql = new QueryRequest
        {
            Query = @"mutation Mutate {
                addMovie(price: 150 rating: ""this is FieldToResolve long"") {
                    id
                }
            }",
        };

        var testContext = new ValidationTestsContext();
        var results = schema.ExecuteRequest(gql, testContext, null, null);
        Assert.NotNull(results.Errors);
        Assert.Equal(5, results.Errors.Count);
        Assert.Equal("Field 'addMovie' - Title is required", results.Errors[0].Message);
        Assert.Equal("Field 'addMovie' - Date is required", results.Errors[1].Message);
        Assert.Equal("Field 'addMovie' - Genre must be specified", results.Errors[2].Message);
        Assert.Equal("Field 'addMovie' - Price must be between $1 and $100", results.Errors[3].Message);
        Assert.Equal("Field 'addMovie' - Rating must be less than 5 characters", results.Errors[4].Message);
    }
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
}

[MutationArguments]
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
}