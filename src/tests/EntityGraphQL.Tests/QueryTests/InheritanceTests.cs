using Xunit;
using EntityGraphQL.Compiler;
using EntityGraphQL.Tests.ApiVersion1;
using EntityGraphQL.Schema;
using System.Linq;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

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
        public void TestInheritancDuplicateFields()
        {
            var schemaProvider = new TestAbstractDataGraphSchema();
            var gql = new GraphQLCompiler(schemaProvider).Compile(@"
                query {
                    animals {
                        __typename
                        id        
                        ... on Cat {
                            id
                        }
                        ...on Dog {
                            id
                        }
                    }
                }
            ");

            var context = new TestAbstractDataContext();
            context.Animals.Add(new Dog() { Id = 1, Name = "steve", HasBone = true });
            context.Animals.Add(new Cat() { Id = 2, Name = "george", Lives = 9 });

            var qr = gql.ExecuteQuery(context, null, null);
            dynamic animals = qr.Data["animals"];
            // we only have the fields requested
            Assert.Equal(2, animals.Count);

            Assert.Equal("Dog", animals[0].__typename);
            Assert.Equal(1, animals[0].id);
            Assert.Equal("Cat", animals[1].__typename);
            Assert.Equal(2, animals[1].id);
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

        [Fact]
        public void SupportsFragmentRepeatedFields()
        {
            // apollo client inserts __typename everywhere
            var schemaProvider = new TestAbstractDataGraphSchema();
            var gql = new GraphQLCompiler(schemaProvider).Compile(@"
                query {
                    dog(id: 9) {
                        ...animalFragment
                        __typename # type name on Dog type
                    }    
                }
                # using a fragment on the base type so it could be reused on other types
                fragment animalFragment on Animal {
                    __typename # type name on base Animal type
                    name # also this builds p_animal.Name where we need p_dog.Name
                }
            ");

            var context = new TestAbstractDataContext();
            context.Dogs.Add(new Dog() { Id = 9, Name = "steve", HasBone = true });

            var qr = gql.ExecuteQuery(context, null, null);
            Assert.Null(qr.Errors);
            dynamic animal = qr.Data["dog"];
            Assert.Equal("Dog", animal.__typename);
            Assert.Equal("steve", animal.name);
        }

        [Fact]
        public void SelectFieldFromInheritedType()
        {
            var schemaProvider = new TestAbstractDataGraphSchema();
            var gql = new GraphQLCompiler(schemaProvider).Compile(@"
                query {
                    dogs {
                        ...dogFragment
                    }    
                }

                fragment dogFragment on Dog {
                    name 
                }
            ");

            var context = new TestAbstractDataContext();
            context.Dogs.Add(new Dog() { Id = 9, Name = "steve", HasBone = true });

            var qr = gql.ExecuteQuery(context, null, null);
            Assert.Null(qr.Errors);
            dynamic animal = Enumerable.First((dynamic)qr.Data["dogs"]);
            Assert.Equal("steve", animal.name);
        }

        [Fact]
        public void SelectFieldFromInheritedTypeWithServiceField()
        {
            var schemaProvider = new TestAbstractDataGraphSchema();
            var gql = new GraphQLCompiler(schemaProvider).Compile(@"
                fragment frag on Dog {
                    name
                }

                query {
                    people {
                        age
                        dogs {
                            ...frag
                        }
                    }
                }
            ");

            var context = new TestAbstractDataContext();
            context.People.Add(new PersonType()
            {
                Id = 1,
                Name = "emma",
                Birthday = DateTime.Now.AddYears(-30),
                Dogs = new List<Dog> { new Dog { Id = 9, Name = "steve", HasBone = true } }
            });
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<AgeService>();

            var qr = gql.ExecuteQuery(context, serviceCollection.BuildServiceProvider(), null);
            Assert.Null(qr.Errors);
            dynamic animal = Enumerable.First(Enumerable.First((dynamic)qr.Data["people"]).dogs);
            Assert.Equal("steve", animal.name);
        }
    }
}