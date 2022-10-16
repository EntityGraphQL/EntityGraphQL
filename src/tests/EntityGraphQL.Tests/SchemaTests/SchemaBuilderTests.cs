using Xunit;
using System.Collections.Generic;
using System;
using EntityGraphQL.Schema;
using System.Linq;

namespace EntityGraphQL.Tests
{
    /// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
    public class SchemaBuilderTests
    {
        [Fact]
        public void ReadsContextType()
        {
            var schema = SchemaBuilder.FromObject<TestEntity>();
            Assert.Equal(typeof(TestEntity), schema.QueryContextType);
        }
        [Fact]
        public void CachesPublicProperties()
        {
            var schema = SchemaBuilder.FromObject<TestEntity>();
            Assert.True(schema.GetSchemaType(typeof(TestEntity), null).HasField("id", null));
            Assert.True(schema.GetSchemaType(typeof(TestEntity), null).HasField("field1", null));
            Assert.True(schema.GetSchemaType(typeof(TestEntity), null).HasField("relation", null));
            Assert.False(schema.GetSchemaType(typeof(TestEntity), null).HasField("notthere", null));
        }
        [Fact]
        public void CachesPublicFields()
        {
            var schema = SchemaBuilder.FromObject<Person>();
            Assert.True(schema.GetSchemaType(typeof(Person), null).HasField("id", null));
            Assert.True(schema.GetSchemaType(typeof(Person), null).HasField("name", null));
        }
        [Fact]
        public void CachesRecursively()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            Assert.True(schema.GetSchemaType(typeof(TestSchema), null).HasField("someRelation", null));
            Assert.True(schema.GetSchemaType(typeof(Person), null).HasField("name", null));
            Assert.True(schema.GetSchemaType(typeof(TestEntity), null).HasField("field1", null));
        }
        [Fact]
        public void AllowsExtending()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            schema.Type<Person>().AddField("idAndName", p => p.Id + " " + p.Name, "The Id and Name");
            Assert.True(schema.Type<Person>().HasField("name", null));
            Assert.True(schema.Type<Person>().HasField("idAndName", null));
        }
        [Fact]
        public void CanNotOverrideExistingType()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                // Type "person" was auto created from the TestSchema
                var t = schema.AddType<Person>("Person", description: "duplicate type");
                t.AddField(p => p.Id, "The unique identifier");
                t.AddField(p => p.Name + " Fakey", "Person's full name");
            });
            Assert.Equal("An item with the same key has already been added. Key: Person", ex.Message);
        }

        [Fact]
        public void AutoAddArgumentForId()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            var argumentTypes = schema.Type<TestSchema>().GetField("person", null).Arguments;
            Assert.Single(argumentTypes);
            Assert.Equal("id", argumentTypes.First().Key);
            Assert.Equal(typeof(int), argumentTypes.First().Value.Type.TypeDotnet);
            Assert.True(argumentTypes.First().Value.Type.TypeNotNullable);
        }
        [Fact]
        public void AutoAddArgumentForIdBase()
        {
            var schema = SchemaBuilder.FromObject<TestSchema>();
            var argumentTypes = schema.Type<TestSchema>().GetField("project", null).Arguments;
            Assert.Single(argumentTypes);
            Assert.Equal("id", argumentTypes.First().Key);
            Assert.Equal(typeof(Guid), argumentTypes.First().Value.Type.TypeDotnet);
            Assert.True(argumentTypes.First().Value.Type.TypeNotNullable);
        }
        [Fact]
        public void AutoAddArgumentForIdGuid()
        {
            var schema = SchemaBuilder.FromObject<TestSchema2>();
            var argumentTypes = schema.Type<TestSchema2>().GetField("property", null).Arguments;
            Assert.Single(argumentTypes);
            Assert.Equal("id", argumentTypes.First().Key);
            Assert.Equal(typeof(Guid), argumentTypes.First().Value.Type.TypeDotnet);
            Assert.True(argumentTypes.First().Value.Type.TypeNotNullable);
        }
        [Fact]
        public void DoesNotSupportSameFieldDifferentArguments()
        {
            // Grpahql doesn't support "field overloading"
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>();
            // user(id: ID) already created
            var ex = Assert.Throws<EntityQuerySchemaException>(() => schemaProvider.Query().AddField("people", new { monkey = ArgumentHelper.Required<int>() }, (ctx, param) => ctx.People.Where(u => u.Id == param.monkey).FirstOrDefault(), "Return a user by ID"));
            Assert.Equal("Field people already exists on type Query. Use ReplaceField() if this is intended.", ex.Message);
        }

        [Fact]
        public void AbstractClassesBecomeInterfaces()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema3>();
            Assert.Equal(GqlTypeEnum.Interface, schemaProvider.Type<AbstractClass>().GqlType);
            Assert.Equal(2, schemaProvider.Type<AbstractClass>().GetFields().Count());

            schemaProvider.AddType<InheritedClass>("InheritedClass");
            Assert.Equal(GqlTypeEnum.Object, schemaProvider.Type<InheritedClass>().GqlType);
            Assert.Single(schemaProvider.Type<InheritedClass>().GetFields());
        }

        [Fact]
        public void AbstractClassesBecomeInterfacesIntrospection()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema3>();

            var gql = new QueryRequest
            {
                Query = @"
        query IntrospectionQuery {
          __type(name: ""AbstractClass"") {
            name
            kind
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>()
            };

            var res = schemaProvider.ExecuteRequest(gql, new TestSchema3(), null, null);
            Assert.Null(res.Errors);

            Assert.Equal("AbstractClass", ((dynamic)res.Data["__type"]).name);
            Assert.Equal("INTERFACE", ((dynamic)res.Data["__type"]).kind);
        }


        [Fact]
        public void InheritedClassesBecomeObjectsIntrospection()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema3>();
            schemaProvider.AddType<InheritedClass>("").ImplementAllBaseTypes();
            Assert.Equal(GqlTypeEnum.Object, schemaProvider.Type<InheritedClass>().GqlType);
            Assert.Single(schemaProvider.Type<InheritedClass>().GetFields());

            var gql = new QueryRequest
            {
                Query = @"
                    query IntrospectionQuery {
                      __type(name: ""InheritedClass"") {
                        name
                        kind
                        interfaces {
                            name
                            kind
                        }
                      }
                    }"
            };

            var res = schemaProvider.ExecuteRequest(gql, new TestSchema3(), null, null);
            Assert.Null(res.Errors);

            Assert.Equal("InheritedClass", ((dynamic)res.Data["__type"]).name);
            Assert.Equal("OBJECT", ((dynamic)res.Data["__type"]).kind);

            Assert.Equal("INTERFACE", ((dynamic)res.Data["__type"]).interfaces[0].kind);
            Assert.Equal("AbstractClass", ((dynamic)res.Data["__type"]).interfaces[0].name);
        }

        [Fact]
        public void NonAbstractClassesBecomeObjectsIntrospection()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema2>();

            var gql = new QueryRequest
            {
                Query = @"
        query IntrospectionQuery {
          __type(name: ""Property"") {
            name
            kind
          }
        }"
            };

            var res = schemaProvider.ExecuteRequest(gql, new TestSchema2(), null, null);
            Assert.Null(res.Errors);

            Assert.Equal("Property", ((dynamic)res.Data["__type"]).name);
            Assert.Equal("OBJECT", ((dynamic)res.Data["__type"]).kind);
        }

        [Fact]
        public void InterfacesWithNoFieldsBecomeUnions()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema4>(new SchemaBuilderOptions() { AutoCreateInterfaceTypes = true });
            Assert.Equal(GqlTypeEnum.Union, schemaProvider.Type<IUnion>().GqlType);
            Assert.Single(schemaProvider.Type<IUnion>().GetFields()); //__typename only
            Assert.Equal("__typename", schemaProvider.Type<IUnion>().GetFields().First().Name); //__typename only
        }

        [Fact]
        public void InterfacesWithNoFieldsBecomeUnions_SDL()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema4>(new SchemaBuilderOptions() { AutoCreateInterfaceTypes = true });
            var schema = schemaProvider.ToGraphQLSchemaString();
            Assert.Contains(@"union: [IUnion!]", schema);

            //no subtypes so not added
            Assert.DoesNotContain(@"union IUnion", schema);
        }

        [Fact]
        public void InterfacesWithNoFieldsBecomeUnions_SDL_WithType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema4>(new SchemaBuilderOptions() { AutoCreateInterfaceTypes = true });
            schemaProvider.Type<IUnion>().AddPossibleType<Person>();
            schemaProvider.Type<IUnion>().AddPossibleType<Property>();

            var schema = schemaProvider.ToGraphQLSchemaString();
            Assert.Contains(@"union: [IUnion!]", schema);

            //no subtypes so not added
            Assert.Contains(@"union IUnion = Person | Property", schema);
        }

        [Fact]
        public void InterfacesWithNoFieldsBecomeUnions_SDL_WithMultipleType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema4>(new SchemaBuilderOptions() { AutoCreateInterfaceTypes = true });
            schemaProvider.Type<IUnion>().AddPossibleType<Person>();

            var schema = schemaProvider.ToGraphQLSchemaString();
            Assert.Contains(@"union: [IUnion!]", schema);

            //no subtypes so not added
            Assert.Contains(@"union IUnion = Person", schema);
        }

        [Fact]
        public void InterfacesWithNoFieldsBecomeUnions_Introspection()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema4>(new SchemaBuilderOptions() { AutoCreateInterfaceTypes = true });

            var gql = new QueryRequest
            {
                Query = @"
        query IntrospectionQuery {
          __type(name: ""IUnion"") {
            name
            kind
          }
        }"
            };

            var res = schemaProvider.ExecuteRequest(gql, new TestSchema4(), null, null);
            Assert.Null(res.Errors);

            Assert.Equal("IUnion", ((dynamic)res.Data["__type"]).name);
            Assert.Equal("UNION", ((dynamic)res.Data["__type"]).kind);
        }

        [Fact]
        public void InterfacesWithNoFieldsBecomeUnions_Introspection_WithType()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema4>(new SchemaBuilderOptions() { AutoCreateInterfaceTypes = true });
            schemaProvider.Type<IUnion>().AddPossibleType<Person>();

            var gql = new QueryRequest
            {
                Query = @"
        query IntrospectionQuery {
          __type(name: ""IUnion"") {
            name
            kind
            possibleTypes { name }
          }
        }"
            };

            var res = schemaProvider.ExecuteRequest(gql, new TestSchema4(), null, null);
            Assert.Null(res.Errors);

            Assert.Equal("IUnion", ((dynamic)res.Data["__type"]).name);
            Assert.Equal("UNION", ((dynamic)res.Data["__type"]).kind);
            Assert.Equal("Person", ((dynamic)res.Data["__type"]).possibleTypes[0].name);
        }

        [Fact]
        public void UnionsCantHaveFields()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema4>(new SchemaBuilderOptions() { AutoCreateInterfaceTypes = true });
            Assert.Throws<InvalidOperationException>(() => schemaProvider.Type<IUnion>().AddField("test", "description"));
        }
        [Fact]
        public void TestIgnoreReferencedTypes()
        {
            var schemaBuilderOptions = new SchemaBuilderOptions
            {
                IgnoreTypes = new HashSet<Type> { typeof(C) }
            };

            var schemaProvider = new SchemaProvider<TestIgnoreTypesSchema>();
            schemaProvider.AddType<B>(typeof(B).Name, null).AddAllFields(schemaBuilderOptions);
            schemaProvider.Query().AddAllFields(schemaBuilderOptions);
            schemaProvider.UpdateType<A>(type => type.AddField("b", null).Resolve(a => new B()));

            Assert.True(schemaProvider.HasType(typeof(A)));
            Assert.True(schemaProvider.HasType(typeof(B)));
            Assert.False(schemaProvider.HasType(typeof(C)));
            Assert.False(schemaProvider.HasType(typeof(D)));
        }

        [Fact]
        public void TestGraphQLFieldAttribute()
        {
            var schemaProvider = SchemaBuilder.FromObject<ContextFieldWithMethod>();

            Assert.True(schemaProvider.HasType(typeof(TypeWithMethod)));

            Assert.True(schemaProvider.Type<TypeWithMethod>().HasField("methodField", null));

            var sdl = schemaProvider.ToGraphQLSchemaString();

            Assert.Contains("fields: [TypeWithMethod!]", sdl);
            Assert.Contains("methodField: Int!", sdl);

            var gql = new QueryRequest
            {
                Query = @"
                    query TypeWithMethod {
                      fields {
                        methodField
                      }
                    }"
            };

            var context = new ContextFieldWithMethod
            {
                Fields = new List<TypeWithMethod>()
                {
                    new TypeWithMethod()
                }
            };

            var res = schemaProvider.ExecuteRequest(gql, context, null, null);
            Assert.Null(res.Errors);

            Assert.Equal(1, ((dynamic)res.Data["fields"])[0].methodField);
        }

        [Fact]
        public void TestGraphQLFieldAttributeWithArgs()
        {
            var schemaProvider = SchemaBuilder.FromObject<ContextFieldWithMethod>();

            Assert.True(schemaProvider.HasType(typeof(TypeWithMethod)));

            Assert.True(schemaProvider.Type<TypeWithMethod>().HasField("methodFieldWithArgs", null));

            var sdl = schemaProvider.ToGraphQLSchemaString();

            Assert.Contains("fields: [TypeWithMethod!]", sdl);
            Assert.Contains("methodFieldWithArgs(value: Int!): Int!", sdl);

            var gql = new QueryRequest
            {
                Query = @"
                    query TypeWithMethod($value: Int) {
                      fields {
                        methodFieldWithArgs(value: $value)
                      }
                    }",
                Variables = new QueryVariables {
                    { "value", 13 }
                }
            };

            var context = new ContextFieldWithMethod
            {
                Fields = new List<TypeWithMethod>()
                {
                    new TypeWithMethod()
                }
            };

            var res = schemaProvider.ExecuteRequest(gql, context, null, null);
            Assert.Null(res.Errors);

            Assert.Equal(13, ((dynamic)res.Data["fields"])[0].methodFieldWithArgs);
        }

        [Fact]
        public void TestGraphQLFieldAttributeWithTwoArgs()
        {
            var schemaProvider = SchemaBuilder.FromObject<ContextFieldWithMethod>();

            Assert.True(schemaProvider.HasType(typeof(TypeWithMethod)));

            Assert.True(schemaProvider.Type<TypeWithMethod>().HasField("methodFieldWithTwoArgs", null));

            var sdl = schemaProvider.ToGraphQLSchemaString();

            Assert.Contains("fields: [TypeWithMethod!]", sdl);
            Assert.Contains("methodFieldWithTwoArgs(value: Int!, value2: Int!): Int!", sdl);

            var gql = new QueryRequest
            {
                Query = @"
                    query TypeWithMethod($value: Int, $value2: Int) {
                      fields {
                        methodFieldWithTwoArgs(value: $value, value2: $value2)
                      }
                    }",
                Variables = new QueryVariables {
                    { "value", 6 },
                    { "value2", 7 },
                }
            };

            var context = new ContextFieldWithMethod
            {
                Fields = new List<TypeWithMethod>()
                {
                    new TypeWithMethod()
                }
            };

            var res = schemaProvider.ExecuteRequest(gql, context, null, null);
            Assert.Null(res.Errors);

            Assert.Equal(13, ((dynamic)res.Data["fields"])[0].methodFieldWithTwoArgs);
        }

        [Fact]
        public void TestGraphQLFieldAttributeWithDefaultArgs()
        {
            var schemaProvider = SchemaBuilder.FromObject<ContextFieldWithMethod>();

            Assert.True(schemaProvider.HasType(typeof(TypeWithMethod)));

            Assert.True(schemaProvider.Type<TypeWithMethod>().HasField("methodFieldWithDefaultArgs", null));

            var sdl = schemaProvider.ToGraphQLSchemaString();

            Assert.Contains("fields: [TypeWithMethod!]", sdl);
            Assert.Contains("methodFieldWithDefaultArgs(value: Int! = 27): Int!", sdl);

            var gql = new QueryRequest
            {
                Query = @"
                    query TypeWithMethod {
                      fields {
                        methodFieldWithDefaultArgs
                      }
                    }"
            };

            var context = new ContextFieldWithMethod
            {
                Fields = new List<TypeWithMethod>()
                {
                    new TypeWithMethod()
                }
            };

            var res = schemaProvider.ExecuteRequest(gql, context, null, null);
            Assert.Null(res.Errors);

            Assert.Equal(27, ((dynamic)res.Data["fields"])[0].methodFieldWithDefaultArgs);
        }

        [Fact]
        public void TestGraphQLFieldAttributeRename()
        {
            var schemaProvider = SchemaBuilder.FromObject<ContextFieldWithMethod>();

            Assert.True(schemaProvider.HasType(typeof(TypeWithMethod)));

            Assert.True(schemaProvider.Type<TypeWithMethod>().HasField("methodFieldWithDefaultArgs", null));

            var sdl = schemaProvider.ToGraphQLSchemaString();

            Assert.Contains("fields: [TypeWithMethod!]", sdl);
            Assert.Contains("renamedMethod: Int!", sdl);
            Assert.DoesNotContain("unknownName", sdl);

            var gql = new QueryRequest
            {
                Query = @"
                    query TypeWithMethod {
                      fields {
                        renamedMethod
                      }
                    }"
            };

            var context = new ContextFieldWithMethod
            {
                Fields = new List<TypeWithMethod>()
                {
                    new TypeWithMethod()
                }
            };

            var res = schemaProvider.ExecuteRequest(gql, context, null, null);
            Assert.Null(res.Errors);

            Assert.Equal(33, ((dynamic)res.Data["fields"])[0].renamedMethod);
        }

        [Fact]
        public void TestGraphQLFieldAttributeOnContext()
        {
            var schemaProvider = SchemaBuilder.FromObject<ContextFieldWithMethod>();

            Assert.True(schemaProvider.Query().HasField("testMethod", null));

            var sdl = schemaProvider.ToGraphQLSchemaString();

            Assert.Contains("testMethod: Int!", sdl);

            var gql = new QueryRequest
            {
                Query = @"
                    query TypeWithMethod {
                      testMethod
                    }"
            };

            var context = new ContextFieldWithMethod
            {
                Fields = new List<TypeWithMethod>()
                {
                    new TypeWithMethod()
                }
            };

            var res = schemaProvider.ExecuteRequest(gql, context, null, null);
            Assert.Null(res.Errors);

            Assert.Equal(23, ((dynamic)res.Data["testMethod"]));
        }

        public class ContextFieldWithMethod {
            public IEnumerable<TypeWithMethod> Fields { get; set; }

            [GraphQLField]
            public int TestMethod()
            {
                return 23;
            }
        }
        public class TypeWithMethod
        {
            [GraphQLField]
            public int MethodField()
            {
                return 1;
            }

            [GraphQLField]
            public int MethodFieldWithArgs(int value)
            {
                return value;
            }

            [GraphQLField]
            public int MethodFieldWithTwoArgs(int value, int value2)
            {
                return value + value2;
            }

            [GraphQLField]
            public int MethodFieldWithDefaultArgs(int value = 27)
            {
                return value;
            }

            [GraphQLField("renamedMethod")]
            public int UnknownName()
            {
                return 33;
            }
        }

        private class TestIgnoreTypesSchema
        {
            public IEnumerable<A> As { get; }
        }
        private class A
        {
            public int I = 0;
        }
        private class B
        {
            public int I = 0;
            public C C = new();
        }
        private class C
        {
            public D D = new();
        }
        private class D
        {
            public int I = 0;
        }

        // This would be your Entity/Object graph you use with EntityFramework
        private class TestSchema
        {
            public TestEntity SomeRelation { get; }
            public IEnumerable<Person> People { get; }
            public IEnumerable<IdInherited> Projects { get; }
        }

        private class IdInherited : HasId, ISomething
        {

        }

        private interface IUnion
        {
        }

        private interface ISomething
        {
            string Name { get; }
        }

        private abstract class HasId
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
        }

        private class TestSchema2
        {
            public IEnumerable<Property> Properties { get; }
        }

        private class TestSchema3
        {
            public IEnumerable<AbstractClass> AbstractClasses { get; }
        }

        private class TestSchema4
        {
            public IEnumerable<IUnion> Union { get; }
        }

        private abstract class AbstractClass
        {
            public int Field1 { get; }
        }

        private class InheritedClass : AbstractClass
        {
            public int Field2 { get; }
        }

        private class TestEntity : IUnion
        {
            public int Id { get; }
            public int Field1 { get; }
            public Person Relation { get; }
        }

        private class Person : IUnion
        {
            public int Id = 0;
            public string Name = string.Empty;
        }

        private class Property : IUnion
        {
            public Guid Id { get; set; }
        }
    }
}