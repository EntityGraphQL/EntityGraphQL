using EntityGraphQL.Schema;
using System.Collections.Generic;
using Xunit;

namespace EntityGraphQL.Tests
{
    public class StarWarsInterfaceTest
    {
        public abstract class Character
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public IEnumerable<Character> Friends { get; set; }
        }

        public class Human : Character
        {
            public int TotalCredits { get; set; }
        }

        public class Droid : Character
        {
            public string PrimaryFunction { get; set; }
        }

        public class StarWarsContext
        {
            public IList<Character> Characters { get; set; }
        }


        [Fact]
        public void StarWarsInterfaceTest_ManualCreation()
        {
            var schema = new SchemaProvider<StarWarsContext>();

            schema.AddInterface<Character>(name: "Character", description: "represents any character in the Star Wars trilogy")
            .AddAllFields();

            schema.AddType<Human>("")
                .AddAllFields()
                .Implements<Character>();

            schema.AddType<Droid>("")
                .Implements<Character>();

            var sdl = schema.ToGraphQLSchemaString();

            Assert.Contains("interface Character", sdl);
            Assert.Contains("type Human implements Character", sdl);
            Assert.Contains("type Droid implements Character", sdl);
        }

        [Fact]
        public void StarWarsInterfaceTest_AutoCreation()
        {
            var schema = SchemaBuilder.FromObject<StarWarsContext>();
            schema.AddType<Human>("Human").AddAllFields().ImplementAllBaseTypes();
            schema.AddType<Droid>("Droid").AddAllFields().ImplementAllBaseTypes();

            var sdl = schema.ToGraphQLSchemaString();

            Assert.Contains("interface Character", sdl);
            Assert.Contains("type Human implements Character", sdl);
            Assert.Contains("type Droid implements Character", sdl);
        }
    }
}
