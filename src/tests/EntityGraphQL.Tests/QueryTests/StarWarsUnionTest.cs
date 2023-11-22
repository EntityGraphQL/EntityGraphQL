using EntityGraphQL.Schema;
using System.Collections.Generic;
using Xunit;

namespace EntityGraphQL.Tests
{
    public class StarWarsUnionTest
    {
        public abstract class Character
        {

        }

        public class Human : Character
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public IEnumerable<Character> Friends { get; set; }
            public int TotalCredits { get; set; }
        }

        public class Droid : Character
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public IEnumerable<Character> Friends { get; set; }
            public string PrimaryFunction { get; set; }
        }

        public class StarWarsContext
        {
            public IList<Character> Characters { get; set; }
        }


        [Fact]
        public void StarWarsUnionTest_ManualCreation()
        {
            var schema = new SchemaProvider<StarWarsContext>();

            schema.AddUnion<Character>(name: "Character", description: "represents any character in the Star Wars trilogy");
            schema.Type<Character>().AddPossibleType<Human>();
            schema.Type<Character>().AddPossibleType<Droid>();

            var sdl = schema.ToGraphQLSchemaString();

            Assert.Contains("union Character = Human | Droid", sdl);
            Assert.Contains("type Human {", sdl);
            Assert.Contains("type Droid {", sdl);
        }

        [Fact]
        public void StarWarsUnionTest_AutoCreation()
        {
            var schema = SchemaBuilder.FromObject<StarWarsContext>();
            schema.Type<Character>().AddAllPossibleTypes();

            var sdl = schema.ToGraphQLSchemaString();

            Assert.Contains("union Character = Human | Droid", sdl);
            Assert.Contains("type Human {", sdl);
            Assert.Contains("type Droid {", sdl);
        }

        [Fact]
        public void AddAllPossibleTypesWithExistingType_266()
        {
            var schema = SchemaBuilder.FromObject<StarWarsContext>();
            schema.AddType<Human>("Human", "A human").AddAllFields();
            schema.Type<Character>().AddAllPossibleTypes();

            var sdl = schema.ToGraphQLSchemaString();

            Assert.Contains("union Character = Human | Droid", sdl);
            Assert.Contains("type Human {", sdl);
            Assert.Contains("type Droid {", sdl);
        }
    }
}
