using Xunit;
using System.Linq;
using EntityGraphQL.Schema;
using System.Linq.Expressions;
using System;
using System.Collections.Generic;
using System.Collections;

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
            dynamic results = schemaProvider.ExecuteRequest(gql, new IgnoreTestSchema(), null, null).Errors;
            var err = Enumerable.First(results);
            Assert.Equal("Field 'movies' not found on type 'Query'", err.Message);
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
            var results = schemaProvider.ExecuteRequest(gql, new IgnoreTestSchema(), null, null);
            Assert.Empty((IEnumerable)results.Data["albums"]);
        }

        [Fact]
        public void TestIgnoreInputFails()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationsFrom(new IgnoreTestMutations());
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
            var results = schemaProvider.ExecuteRequest(gql, new IgnoreTestSchema(), null, null);
            var error = results.Errors.First();
            Assert.Equal("No argument 'hiddenInputField' found on field 'addAlbum'", error.Message);
        }

        [Fact]
        public void TestIgnoreInputPasses()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationsFrom(new IgnoreTestMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest
            {
                Query = @"mutation Test($name: String) {
  addAlbum(name: $name genre: ""Rock"") {
    id name hiddenInputField
  }
}",
                Variables = new QueryVariables {
                    {"name", "Balance, Not Symmetry"},
                }
            };
            var results = schemaProvider.ExecuteRequest(gql, new IgnoreTestSchema(), null, null);
            Assert.Null(results.Errors);
            dynamic data = results.Data["addAlbum"];
            Assert.Equal("Balance, Not Symmetry", data.name);
            Assert.Null(data.hiddenInputField); // not hidden from query
            Assert.InRange(data.id, 0, 100);
        }

        [Fact]
        public void TestIgnoreAllInInput()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationsFrom(new IgnoreTestMutations());
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
            var results = schemaProvider.ExecuteRequest(gql, new IgnoreTestSchema(), null, null);
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
            var results = schemaProvider.ExecuteRequest(gql, new IgnoreTestSchema(), null, null);
            var error = results.Errors.First();
            Assert.Equal("Field 'hiddenField' not found on type 'Album'", error.Message);
        }

        [Fact]
        public void TestIgnoreWithSchema()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationsFrom(new IgnoreTestMutations());
            schemaProvider.Type<Album>().RemoveField("old");
            var schema = schemaProvider.ToGraphQLSchemaString();
            Assert.DoesNotContain("hiddenField", schema);
            // this exists as it is available for querying
            //Assert.Contains("type Album {\n\tid: Int!\n\tname: String!\n\thiddenInputField: String\n\tgenre: Genre!\n}", schema);
            Assert.Contains(@"type Album {
	id: Int!
	name: String!
	hiddenInputField: String
	genre: Genre!
}", schema);
            // doesn't include the hidden input fields
            Assert.Contains("addAlbum(name: String!, genre: Genre!): Album", schema);
        }

        [Fact]
        public void TestMutationWithListReturnType()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
            schemaProvider.AddMutationsFrom(new IgnoreTestMutations());
            var schema = schemaProvider.ToGraphQLSchemaString();
            Assert.Contains("addAlbum2(name: String!, genre: Genre!): [Album!]", schema);
        }

        [Fact]
        public void TestNotNullTypes()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.Type<Album>().RemoveField("old");
            schemaProvider.AddMutationsFrom(new IgnoreTestMutations());
            var schema = schemaProvider.ToGraphQLSchemaString();
            // this exists as it is not null
            Assert.Contains(@"type Album {
	id: Int!
	name: String!
	hiddenInputField: String
	genre: Genre!
}", schema);
        }
        [Fact]
        public void TestNullableEnumInType()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            var schema = schemaProvider.ToGraphQLSchemaString();
            // this exists as it is not null
            Assert.Contains(@"type Artist {
	id: Int!
	type: ArtistType
}", schema);
        }
        [Fact]
        public void TestNotNullArgs()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationsFrom(new IgnoreTestMutations());
            var schema = schemaProvider.ToGraphQLSchemaString();
            // this exists as it is not null
            Assert.Contains("addAlbum(name: String!, genre: Genre!): Album", schema);
        }
        [Fact]
        public void TestNotNullEnumerableElementByDefault()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationsFrom(new IgnoreTestMutations());
            var schema = schemaProvider.ToGraphQLSchemaString();
            // this exists as it is not null
            Assert.Contains("albums: [Album!]", schema);
        }
        [Fact]
        public void TestNullEnumerableElement()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationsFrom(new IgnoreTestMutations());
            var schema = schemaProvider.ToGraphQLSchemaString();
            // this exists as it is not null
            Assert.Contains("nullAlbums: [Album]", schema);
        }

        [Fact]
        public void TestDeprecatedField()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            var schema = schemaProvider.ToGraphQLSchemaString();
            // this exists as it is not null
            Assert.Contains("old: Int! @deprecated(reason: \"because\")", schema);
        }
        [Fact]
        public void TestDeprecatedMutationField()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationsFrom(new IgnoreTestMutations());
            var schema = schemaProvider.ToGraphQLSchemaString();
            // this exists as it is not null
            Assert.Contains("addAlbumOld(name: String!, genre: Genre!): Album @deprecated(reason: \"This is obsolete\")", schema);
        }
        [Fact]
        public void TestDeprecatedEnumField()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            var schema = schemaProvider.ToGraphQLSchemaString();
            // this exists as it is not null
            Assert.Contains("Obsolete @deprecated(reason: \"This is an obsolete genre\")", schema);
        }

        [Fact]
        public void TestNullableRefTypeMutationField()
        {
            var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>(false);
            schemaProvider.AddMutationsFrom(new NullableRefTypeMutations());
            var schema = schemaProvider.ToGraphQLSchemaString();
            // this exists as it is not null
            Assert.Contains("addAlbum(name: String!, genre: Genre!): Album\r\n", schema);
            Assert.Contains("addAlbum2(name: String!, genre: Genre!): Album!", schema);
            Assert.Contains("addAlbum3(name: String!, genre: Genre!): Album\r\n", schema);
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

        [GraphQLMutation]
        [Obsolete("This is obsolete")]
        public Expression<Func<IgnoreTestSchema, Album>> AddAlbumOld(IgnoreTestSchema db, Album args)
        {
            var newAlbum = new Album
            {
                Id = new Random().Next(100),
                Name = args.Name,
            };
            db.Albums.Add(newAlbum);
            return ctx => ctx.Albums.First(a => a.Id == newAlbum.Id);
        }

    }

    public class NullableRefTypeMutations
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

#nullable enable
        [GraphQLMutation]
        public Expression<Func<IgnoreTestSchema, Album>> AddAlbum2(IgnoreTestSchema db, Album args)
        {
            var newAlbum = new Album
            {
                Id = new Random().Next(100),
                Name = args.Name,
            };
            db.Albums.Add(newAlbum);
            return ctx => ctx.Albums.First(a => a.Id == newAlbum.Id);
        }


        [GraphQLMutation]
        public Expression<Func<IgnoreTestSchema, Album?>> AddAlbum3(IgnoreTestSchema db, Album args)
        {
            var newAlbum = new Album
            {
                Id = new Random().Next(100),
                Name = args.Name,
            };
            db.Albums.Add(newAlbum);
            return ctx => ctx.Albums.First(a => a.Id == newAlbum.Id);
        }
#nullable restore
    }


    public class MovieArgs
    {
        [GraphQLNotNull]
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
        [GraphQLElementTypeNullable]
        public List<Album> NullAlbums { get; set; }
        public List<Artist> Artists { get; set; }
    }

    public enum Genre
    {
        Rock,
        Classical,
        Jazz,
        Alternitive,
        Pop,
        [Obsolete("This is an obsolete genre")]
        Obsolete
    }

    [MutationArguments]
    public class Album
    {
        [GraphQLIgnore(GraphQLIgnoreType.Input)]
        public int Id { get; set; }
        [GraphQLNotNull]
        public string Name { get; set; }
        [GraphQLIgnore(GraphQLIgnoreType.Input)]
        public string HiddenInputField { get; set; }
        [GraphQLIgnore(GraphQLIgnoreType.All)] // default
        public string HiddenAllField { get; set; }
        [GraphQLNotNull]
        public Genre Genre { get; set; }
        [Obsolete("because")]
        [GraphQLIgnore(GraphQLIgnoreType.Input)]
        public int Old { get; set; }
    }

    public class Movie
    {
        public int Id { get; set; }
        public string Title { get; set; }
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