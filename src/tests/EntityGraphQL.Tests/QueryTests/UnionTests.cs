using Xunit;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;
using Microsoft.CSharp.RuntimeBinder;

namespace EntityGraphQL.Tests
{
    public class UnionTests
    {

        [Fact]
        public void TestAutoUnion()
        {
            var schema = SchemaBuilder.FromObject<TestUnionDataContext>(new SchemaBuilderOptions { AutoCreateInterfaceTypes = true });
            Assert.True(schema.HasType(typeof(IAnimal)));
            Assert.True(schema.GetSchemaType(typeof(IAnimal), null).GqlType == GqlTypes.Union);

            schema.Type<IAnimal>().AddPossibleType<Dog>();
            schema.Type<IAnimal>().AddPossibleType<Cat>();
            Assert.True(schema.GetSchemaType(typeof(Cat), null).GqlType == GqlTypes.QueryObject);
            Assert.True(schema.GetSchemaType(typeof(Dog), null).GqlType == GqlTypes.QueryObject);



            var gql = new GraphQLCompiler(schema).Compile(@"
                query {
                    animals {
                        __typename
                        ... on Dog {
                            name
                            hasBone
                        }
                        ... on Cat {
                            name
                            lives
                        }
                    }
                }");
            var context = new TestUnionDataContext();
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
        public void TestAutoUnion_NoTypeName()
        {
            var schema = SchemaBuilder.FromObject<TestUnionDataContext>(new SchemaBuilderOptions { AutoCreateInterfaceTypes = true });
            Assert.True(schema.HasType(typeof(IAnimal)));
            Assert.True(schema.GetSchemaType(typeof(IAnimal), null).GqlType == GqlTypes.Union);

            schema.Type<IAnimal>().AddPossibleType<Dog>();
            schema.Type<IAnimal>().AddPossibleType<Cat>();
            Assert.True(schema.GetSchemaType(typeof(Cat), null).GqlType == GqlTypes.QueryObject);
            Assert.True(schema.GetSchemaType(typeof(Dog), null).GqlType == GqlTypes.QueryObject);



            var gql = new GraphQLCompiler(schema).Compile(@"
query {
    animals {
        ... on Dog {
            name
            hasBone
        }
        ... on Cat {
            name
            lives
        }
    }
}");
            var context = new TestUnionDataContext();
            context.Animals.Add(new Dog() { Name = "steve", HasBone = true });
            context.Animals.Add(new Cat() { Name = "george", Lives = 9 });

            var qr = gql.ExecuteQuery(context, null, null);
            dynamic animals = qr.Data["animals"];
            // we only have the fields requested
            Assert.Equal(2, animals.Count);

            Assert.Throws<RuntimeBinderException>(() => animals[0].__typename);
            Assert.Equal("steve", animals[0].name);
            Assert.True(animals[0].hasBone);
            Assert.Throws<RuntimeBinderException>(() => animals[1].__typename);
            Assert.Equal("george", animals[1].name);
            Assert.Equal(9, animals[1].lives);
        }


        [Fact]
        public void TestAutoUnion_DogOnly()
        {
            var schema = SchemaBuilder.FromObject<TestUnionDataContext>(new SchemaBuilderOptions { AutoCreateInterfaceTypes = true });
            Assert.True(schema.HasType(typeof(IAnimal)));
            Assert.True(schema.GetSchemaType(typeof(IAnimal), null).GqlType == GqlTypes.Union);

            schema.Type<IAnimal>().AddPossibleType<Dog>();
            schema.Type<IAnimal>().AddPossibleType<Cat>();
            Assert.True(schema.GetSchemaType(typeof(Cat), null).GqlType == GqlTypes.QueryObject);
            Assert.True(schema.GetSchemaType(typeof(Dog), null).GqlType == GqlTypes.QueryObject);



            var gql = new GraphQLCompiler(schema).Compile(@"
                query {
                    animals {
                        ... on Dog {
                            name
                            hasBone
                        }
                    }
                }");
            var context = new TestUnionDataContext();
            context.Animals.Add(new Dog() { Name = "steve", HasBone = true });
            context.Animals.Add(new Cat() { Name = "george", Lives = 9 });

            var qr = gql.ExecuteQuery(context, null, null);
            dynamic animals = qr.Data["animals"];
            // we only have the fields requested
            Assert.Equal(2, animals.Count);

            Assert.Equal("steve", animals[0].name);
            Assert.True(animals[0].hasBone);


            //Cats are not null but have 0 fields
            Assert.NotNull(animals[1]);
            Assert.Empty(animals[1].GetType().GetFields());
        }

        [Fact]
        public void TestAutoUnion_CatOnly()
        {
            var schema = SchemaBuilder.FromObject<TestUnionDataContext>(new SchemaBuilderOptions { AutoCreateInterfaceTypes = true });
            Assert.True(schema.HasType(typeof(IAnimal)));
            Assert.True(schema.GetSchemaType(typeof(IAnimal), null).GqlType == GqlTypes.Union);

            schema.Type<IAnimal>().AddPossibleType<Dog>();
            schema.Type<IAnimal>().AddPossibleType<Cat>();
            Assert.True(schema.GetSchemaType(typeof(Cat), null).GqlType == GqlTypes.QueryObject);
            Assert.True(schema.GetSchemaType(typeof(Dog), null).GqlType == GqlTypes.QueryObject);

            var gql = new GraphQLCompiler(schema).Compile(@"
query {
    animals {
        ... on Cat {
            name
            lives
        }
    }
}");
            var context = new TestUnionDataContext();
            context.Animals.Add(new Dog() { Name = "steve", HasBone = true });
            context.Animals.Add(new Cat() { Name = "george", Lives = 9 });

            var qr = gql.ExecuteQuery(context, null, null);
            dynamic animals = qr.Data["animals"];
            // we only have the fields requested
            Assert.Equal(2, animals.Count);

            //Dogs are not null but have 0 fields
            Assert.NotNull(animals[0]);
            Assert.Empty(animals[0].GetType().GetFields());

            Assert.Equal("george", animals[1].name);
            Assert.Equal(9, animals[1].lives);
        }

        [Fact]
        public void TestAutoUnion_TypeOnly()
        {
            var schema = SchemaBuilder.FromObject<TestUnionDataContext>(new SchemaBuilderOptions { AutoCreateInterfaceTypes = true });
            Assert.True(schema.HasType(typeof(IAnimal)));
            Assert.True(schema.GetSchemaType(typeof(IAnimal), null).GqlType == GqlTypes.Union);

            schema.Type<IAnimal>().AddPossibleType<Dog>();
            schema.Type<IAnimal>().AddPossibleType<Cat>();
            Assert.True(schema.GetSchemaType(typeof(Cat), null).GqlType == GqlTypes.QueryObject);
            Assert.True(schema.GetSchemaType(typeof(Dog), null).GqlType == GqlTypes.QueryObject);



            var gql = new GraphQLCompiler(schema).Compile(@"
query {
    animals {
        __typename
    }
}");
            var context = new TestUnionDataContext();
            context.Animals.Add(new Dog() { Name = "steve", HasBone = true });
            context.Animals.Add(new Cat() { Name = "george", Lives = 9 });

            var qr = gql.ExecuteQuery(context, null, null);
            dynamic animals = qr.Data["animals"];
            // we only have the fields requested
            Assert.Equal(2, animals.Count);

            Assert.Equal("Dog", animals[0].__typename);
            Assert.Throws<RuntimeBinderException>(() => animals[0].name);
            Assert.Throws<RuntimeBinderException>(() => animals[0].hasBone);
            Assert.Equal("Cat", animals[1].__typename);
            Assert.Throws<RuntimeBinderException>(() => animals[1].name);
            Assert.Throws<RuntimeBinderException>(() => animals[1].lives);
        }
    }
}
