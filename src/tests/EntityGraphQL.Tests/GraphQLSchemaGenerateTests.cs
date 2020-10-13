using Xunit;
using System.Linq;
using EntityGraphQL.Schema;
using System.Linq.Expressions;
using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// Tests graphql metadata
    /// </summary>
    public class GraphQLSchemaGenerateTests
    {
        [Fact]
        public void TestIgnoreQueryFails()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"query Test { movies { id } }",
            };
            dynamic results = schemaProvider.ExecuteQuery(gql, new IgnoreTestSchema(), null, null).Errors;
            var err = Enumerable.First(results);
            Assert.Equal("Field movies not found on type IgnoreTestSchema", err.Message);
        }
        [Fact]
        public void TestIgnoreQueryPasses()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"query Test { albums { id } }",
            };
            var results = schemaProvider.ExecuteQuery(gql, new IgnoreTestSchema(), null, null);
            Assert.Empty(((IEnumerable)results.Data["albums"]));
        }

        [Fact]
        public void TestIgnoreInputFails()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationFrom(new IgnoreTestMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation Test($name: String, $hiddenInputField: String) {
  addAlbum(name: $name, hiddenInputField: $hiddenInputField) {
    id
  }
}",
                Variables = new QueryVariables {
                    {"name", "Balance, Not Symmetry"},
                    {"hiddenInputField", "yeh"},
                }
            };
            var results = schemaProvider.ExecuteQuery(gql, new IgnoreTestSchema(), null, null);
            var error = results.Errors.First();
            Assert.Equal("No argument 'hiddenInputField' found on field 'addAlbum'", error.Message);
        }

        [Fact]
        public void TestIgnoreInputPasses()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationFrom(new IgnoreTestMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation Test($name: String) {
  addAlbum(name: $name) {
    id name hiddenInputField
  }
}",
                Variables = new QueryVariables {
                    {"name", "Balance, Not Symmetry"},
                }
            };
            var results = schemaProvider.ExecuteQuery(gql, new IgnoreTestSchema(), null, null);
            dynamic data = results.Data["addAlbum"];
            Assert.Equal("Balance, Not Symmetry", data.name);
            Assert.Null(data.hiddenInputField); // not hidden from query
            Assert.InRange(data.id, 0, 100);
        }

        [Fact]
        public void TestIgnoreAllInInput()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationFrom(new IgnoreTestMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation Test($name: String, $hiddenField: String) {
  addAlbum(name: $name, hiddenField: $hiddenField) {
    id
  }
}",
                Variables = new QueryVariables {
                    {"name", "Balance, Not Symmetry"},
                    {"hiddenField", "yeh"},
                }
            };
            var results = schemaProvider.ExecuteQuery(gql, new IgnoreTestSchema(), null, null);
            var error = results.Errors.First();
            Assert.Equal("No argument 'hiddenField' found on field 'addAlbum'", error.Message);
        }

        [Fact]
        public void TestIgnoreAllInQuery()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"query Test {
  albums {
    id hiddenInputField hiddenField
  }
}",
                Variables = new QueryVariables { }
            };
            var results = schemaProvider.ExecuteQuery(gql, new IgnoreTestSchema(), null, null);
            var error = results.Errors.First();
            Assert.Equal("Field hiddenField not found on type Album", error.Message);
        }

        [Fact]
        public void TestIgnoreWithSchema()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationFrom(new IgnoreTestMutations());
            var schema = schemaProvider.GetGraphQLSchema();
            Assert.DoesNotContain("hiddenField", schema);
            // this exists as it is available for querying
            Assert.Contains("type Album {\n\tid: Int!\n\tname: String!\n\thiddenInputField: String\n\tgenre: Genre!\n}", schema);
            // doesn't include the hidden input fields
            Assert.Contains("addAlbum(id: Int!, name: String!, genre: Genre!): Album", schema);
        }

        [Fact]
        public void TestMutationWithListReturnType()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
            schemaProvider.AddMutationFrom(new IgnoreTestMutations());
            var schema = schemaProvider.GetGraphQLSchema();
            Assert.Contains("addAlbum2(id: Int!, name: String!, genre: Genre!): [Album!]", schema);
        }

        [Fact]
        public void TestNotNullTypes()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationFrom(new IgnoreTestMutations());
            var schema = schemaProvider.GetGraphQLSchema();
            // this exists as it is not null
            Assert.Contains("type Album {\n\tid: Int!\n\tname: String!\n\thiddenInputField: String\n\tgenre: Genre!\n}", schema);
        }
        [Fact]
        public void TestNullableEnumInType()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            var schema = schemaProvider.GetGraphQLSchema();
            // this exists as it is not null
            Assert.Contains("type Artist {\n\tid: Int!\n\ttype: ArtistType\n}", schema);
        }
        [Fact]
        public void TestNotNullArgs()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationFrom(new IgnoreTestMutations());
            var schema = schemaProvider.GetGraphQLSchema();
            // this exists as it is not null
            Assert.Contains("addAlbum(id: Int!, name: String!, genre: Genre!): Album", schema);
        }
        [Fact]
        public void TestNotNullEnumerableElementByDefault()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationFrom(new IgnoreTestMutations());
            var schema = schemaProvider.GetGraphQLSchema();
            // this exists as it is not null
            Assert.Contains("albums: [Album!]", schema);
        }
        [Fact]
        public void TestNullEnumerableElement()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationFrom(new IgnoreTestMutations());
            var schema = schemaProvider.GetGraphQLSchema();
            // this exists as it is not null
            Assert.Contains("nullAlbums: [Album]", schema);
        }
    }

    public class IgnoreTestMutations
    {
        [GraphQLMutation]
        public Expression<Func<IgnoreTestSchema, Album>> AddAlbum(IgnoreTestSchema db, Album args)
        {
            var newAlbum = new Album
            {
                Id = new Random().Next(100),
                Name = args.Name,
            };
            db.Albums.Add(newAlbum);
            return ctx => ctx.Albums.First(a => a.Id == newAlbum.Id);
        }

        [GraphQLMutation("Test correct generation of return type for a list")]
        public Expression<Func<IgnoreTestSchema, IEnumerable<Album>>> AddAlbum2(IgnoreTestSchema db, Album args)
        {
            var newAlbum = new Album
            {
                Id = new Random().Next(100),
                Name = args.Name,
            };
            db.Albums.Add(newAlbum);
            return ctx => ctx.Albums;
        }
    }

    public class MovieArgs
    {
        [Required]
        public string Name { get; set; }
        [GraphQLIgnore(GraphQLIgnoreType.Input)]
        public string Hidden { get; set; }
    }

    public class IgnoreTestSchema
    {
        public IgnoreTestSchema()
        {
            Movies = new List<Movie>();
            Albums = new List<Album>();
            NullAlbums = new List<Album>();
        }

        [GraphQLIgnore(GraphQLIgnoreType.Query)]
        public List<Movie> Movies { get; set; }
        public List<Album> Albums { get; set; }
        [Required]
        public List<Album> NullAlbums { get; set; }
        public List<Artist> Artists { get; set; }
    }

    public enum Genre
    {
        Rock,
        Classical,
        Jazz,
        Alternitive,
        Pop
    }

    [MutationArguments]
    public class Album
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        [GraphQLIgnore(GraphQLIgnoreType.Input)]
        public string HiddenInputField { get; set; }
        [GraphQLIgnore(GraphQLIgnoreType.All)] // default
        public string HiddenAllField { get; set; }
        [Required]
        public Genre Genre { get; set; }
    }

    public class Movie
    {
        public int Id { get; set; }
    }

    public enum ArtistType
    {
        Solo,
        Band,
        Supergroup,
    }
    public class Artist
    {
        public int Id { get; set; }
        public ArtistType? Type { get; set; }
    }
}