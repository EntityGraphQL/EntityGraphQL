using Xunit;
using EntityGraphQL.Compiler;
using EntityGraphQL.Tests.ApiVersion1;
using EntityGraphQL.Schema;
using System.Linq.Expressions;
using System.Collections.Generic;

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
        public void TestAutoInheritance()
        {
            var schema = SchemaBuilder.FromObject<TestAbstractDataContextNoAnimals>(new SchemaBuilderOptions { AutoCreateInterfaceTypes = true });
            Assert.True(schema.HasType(typeof(Dog)));
            Assert.True(schema.HasType(typeof(Cat)));
            Assert.True(schema.HasType(typeof(Animal)));
            Assert.True(schema.GetSchemaType(typeof(Animal), null).IsInterface);
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

        [Fact]
        public void TestInheritanceExtraFieldsOnObjectDog()
        {
            var schemaProvider = new TestAbstractDataGraphSchema();
            var gql = new GraphQLCompiler(schemaProvider).Compile(@"
query {
    animal(id: 9) {
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
            context.Animals.Add(new Dog() { Id = 9, Name = "steve", HasBone = true });
            context.Animals.Add(new Cat() { Name = "george", Lives = 9 });

            var qr = gql.ExecuteQuery(context, null, null);
            dynamic animal = qr.Data["animal"];
            Assert.Equal("Dog", animal.__typename);
            Assert.Equal("steve", animal.name);
            Assert.True(animal.hasBone);
            // does not have the cat field
            Assert.Null(animal.GetType().GetField("lives"));
        }

        [Fact]
        public void TestInheritanceExtraFieldsOnObjectCat()
        {
            var schemaProvider = new TestAbstractDataGraphSchema();
            var gql = new GraphQLCompiler(schemaProvider).Compile(@"
query {
    animal(id: 2) {
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
            context.Animals.Add(new Dog() { Id = 9, Name = "steve", HasBone = true });
            context.Animals.Add(new Cat() { Id = 2, Name = "george", Lives = 9 });

            var qr = gql.ExecuteQuery(context, null, null);
            dynamic animal = qr.Data["animal"];
            Assert.Equal("Cat", animal.__typename);
            Assert.Equal("george", animal.name);
            Assert.Equal(9, animal.lives);
            // does not have the dog field
            Assert.Null(animal.GetType().GetField("hasBone"));
        }

        [Fact]
        public void TestInheritanceExtraFieldsOnObjectCatUsingFragments()
        {
            var schemaProvider = new TestAbstractDataGraphSchema();
            var gql = new GraphQLCompiler(schemaProvider).Compile(@"
query {
    animal(id: 2) {
       ...animalFragment
    }    
}

fragment animalFragment on Animal {
    __typename
    name
    ... on Cat {
        lives 
    }
    ...on Dog {
        hasBone 
    }
}
");

            var context = new TestAbstractDataContext();
            context.Animals.Add(new Dog() { Id = 9, Name = "steve", HasBone = true });
            context.Animals.Add(new Cat() { Id = 2, Name = "george", Lives = 9 });

            var qr = gql.ExecuteQuery(context, null, null);
            dynamic animal = qr.Data["animal"];
            Assert.Equal("Cat", animal.__typename);
            Assert.Equal("george", animal.name);
            Assert.Equal(9, animal.lives);
            // does not have the dog field
            Assert.Null(animal.GetType().GetField("hasBone"));
        }


        [Fact]
        public void TestInheritanceReturnFromMutation()
        {
            var schemaProvider = new TestAbstractDataGraphSchema();

            schemaProvider.Mutation().AddFrom<TestAbstractDataContext>();

            var gql = new GraphQLCompiler(schemaProvider).Compile(@"
mutation {
    testMutation(id: 1) {
        id
        ... on Dog {
            hasBone
        }
    }
}
");

            var context = new TestAbstractDataContext();
            context.Animals.Add(new Dog() { Id = 1, Name = "steve", HasBone = true });
            context.Animals.Add(new Cat() { Name = "george", Lives = 9 });

            var qr = gql.ExecuteQuery(context, null, null);
            dynamic animal = qr.Data["testMutation"];
            // we only have the fields requested

            Assert.Equal(1, animal.id);
            Assert.True(animal.hasBone);
        }
    }
}