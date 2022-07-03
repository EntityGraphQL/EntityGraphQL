using Xunit;
using EntityGraphQL.Compiler;
using EntityGraphQL.Tests.ApiVersion1;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// Tests Inheritance & inline fragments
    /// </summary>
    public class InheritanceTests
    {
        [Fact]
        public void TestInheritance()
        {
            var schemaProvider = new TestAbstractDataGraphSchema();
            var gql = new GraphQLCompiler(schemaProvider).Compile(@"
query {
    animals {
        __typename
        name
    }
}");
            var context = new TestAbstractDataContext();
            context.Animals.Add(new Dog() { Name = "steve", HasBone = true });
            context.Animals.Add(new Cat() { Name = "george", Lives = 9 });

            var qr = gql.ExecuteQuery(context, null, null);
            dynamic animals = (dynamic)qr.Data["animals"];
            // we only have the fields requested
            Assert.Equal(2, animals.Count);

            Assert.Equal("Dog", animals[0].__typename);
            Assert.Equal("Cat", animals[1].__typename);
        }

        [Fact]
        public void TestInheritanceExtraFields()
        {
            var schemaProvider = new TestAbstractDataGraphSchema();
            var gql = new GraphQLCompiler(schemaProvider).Compile(@"
query {
    animals {
        __typename
        name
        ... on Cat {
            lives 
        }
        ...on Dog {
            hasBone 
        }
    }
}
");

            var context = new TestAbstractDataContext();
            context.Animals.Add(new Dog() { Name = "steve", HasBone = true });
            context.Animals.Add(new Cat() { Name = "george", Lives = 9 });

            var qr = gql.ExecuteQuery(context, null, null);
            dynamic animals = qr.Data["animals"];
            // we only have the fields requested
            Assert.Equal(2, animals.Count);

            Assert.Equal("Dog", animals[0].__typename);
            Assert.Equal("steve", animals[0].name);
            Assert.True(animals[0].hasBone);
            Assert.Equal("Cat", animals[1].__typename);
            Assert.Equal("george", animals[1].name);
            Assert.Equal(9, animals[1].lives);
        }
    }
}