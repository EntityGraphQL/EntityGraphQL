using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

/// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
public class SchemaBuilderFromObjectTests
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
        Assert.True(schema.GetSchemaType(typeof(TestEntity), false, null).HasField("id", null));
        Assert.True(schema.GetSchemaType(typeof(TestEntity), false, null).HasField("field1", null));
        Assert.True(schema.GetSchemaType(typeof(TestEntity), false, null).HasField("relation", null));
        Assert.False(schema.GetSchemaType(typeof(TestEntity), false, null).HasField("notthere", null));
    }

    [Fact]
    public void CachesPublicFields()
    {
        var schema = SchemaBuilder.FromObject<Person>();
        Assert.True(schema.GetSchemaType(typeof(Person), false, null).HasField("id", null));
        Assert.True(schema.GetSchemaType(typeof(Person), false, null).HasField("name", null));
    }

    [Fact]
    public void CachesRecursively()
    {
        var schema = SchemaBuilder.FromObject<TestSchema>();
        Assert.True(schema.GetSchemaType(typeof(TestSchema), false, null).HasField("someRelation", null));
        Assert.True(schema.GetSchemaType(typeof(Person), false, null).HasField("name", null));
        Assert.True(schema.GetSchemaType(typeof(TestEntity), false, null).HasField("field1", null));
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
        // Graphql doesn't support "field overloading"
        var schemaProvider = SchemaBuilder.FromObject<TestSchema>();
        // user(id: ID) already created
        var ex = Assert.Throws<EntityQuerySchemaException>(
            () =>
                schemaProvider
                    .Query()
                    .AddField("people", new { monkey = ArgumentHelper.Required<int>() }, (ctx, param) => ctx.People.Where(u => u.Id == param.monkey).FirstOrDefault(), "Return a user by ID")
        );
        Assert.Equal("Field 'people' already exists on type 'Query'. Use ReplaceField() if this is intended.", ex.Message);
    }

    [Fact]
    public void AbstractClassesBecomeInterfaces()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestSchema3>();
        Assert.Equal(GqlTypes.Interface, schemaProvider.Type<AbstractClass>().GqlType);
        Assert.Equal(2, schemaProvider.Type<AbstractClass>().GetFields().Count());

        schemaProvider.AddType<InheritedClass>("InheritedClass");
        Assert.Equal(GqlTypes.QueryObject, schemaProvider.Type<InheritedClass>().GqlType);
        Assert.Single(schemaProvider.Type<InheritedClass>().GetFields());
    }

    [Fact]
    public void AbstractClassesBecomeInterfacesIntrospection()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestSchema3>();

        var gql = new QueryRequest
        {
            Query =
                @"
                query IntrospectionQuery {
                    __type(name: ""AbstractClass"") {
                        name
                        kind
                    }
                }"
        };

        var res = schemaProvider.ExecuteRequestWithContext(gql, new TestSchema3(), null, null);
        Assert.Null(res.Errors);

        Assert.Equal("AbstractClass", ((dynamic)res.Data!["__type"]!).name);
        Assert.Equal("INTERFACE", ((dynamic)res.Data!["__type"]!).kind);
    }

    [Fact]
    public void InheritedClassesBecomeObjectsIntrospection()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestSchema3>();
        schemaProvider.AddType<InheritedClass>("").ImplementAllBaseTypes();
        Assert.Equal(GqlTypes.QueryObject, schemaProvider.Type<InheritedClass>().GqlType);
        Assert.Single(schemaProvider.Type<InheritedClass>().GetFields());

        var gql = new QueryRequest
        {
            Query =
                @"
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

        var res = schemaProvider.ExecuteRequestWithContext(gql, new TestSchema3(), null, null);
        Assert.Null(res.Errors);

        Assert.Equal("InheritedClass", ((dynamic)res.Data!["__type"]!).name);
        Assert.Equal("OBJECT", ((dynamic)res.Data!["__type"]!).kind);

        Assert.Equal("INTERFACE", ((dynamic)res.Data!["__type"]!).interfaces[0].kind);
        Assert.Equal("AbstractClass", ((dynamic)res.Data!["__type"]!).interfaces[0].name);
    }

    [Fact]
    public void NonAbstractClassesBecomeObjectsIntrospection()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestSchema2>();

        var gql = new QueryRequest
        {
            Query =
                @"
        query IntrospectionQuery {
          __type(name: ""Property"") {
            name
            kind
          }
        }"
        };

        var res = schemaProvider.ExecuteRequestWithContext(gql, new TestSchema2(), null, null);
        Assert.Null(res.Errors);

        Assert.Equal("Property", ((dynamic)res.Data!["__type"]!).name);
        Assert.Equal("OBJECT", ((dynamic)res.Data!["__type"]!).kind);
    }

    [Fact]
    public void InterfacesWithNoFieldsBecomeUnions()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestSchema4>(new SchemaBuilderOptions() { AutoCreateInterfaceTypes = true });
        Assert.Equal(GqlTypes.Union, schemaProvider.Type<IUnion>().GqlType);
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
            Query =
                @"
        query IntrospectionQuery {
          __type(name: ""IUnion"") {
            name
            kind
          }
        }"
        };

        var res = schemaProvider.ExecuteRequestWithContext(gql, new TestSchema4(), null, null);
        Assert.Null(res.Errors);

        Assert.Equal("IUnion", ((dynamic)res.Data!["__type"]!).name);
        Assert.Equal("UNION", ((dynamic)res.Data!["__type"]!).kind);
    }

    [Fact]
    public void InterfacesWithNoFieldsBecomeUnions_Introspection_WithType()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestSchema4>(new SchemaBuilderOptions() { AutoCreateInterfaceTypes = true });
        schemaProvider.Type<IUnion>().AddPossibleType<Person>();

        var gql = new QueryRequest
        {
            Query =
                @"
        query IntrospectionQuery {
          __type(name: ""IUnion"") {
            name
            kind
            possibleTypes { name }
          }
        }"
        };

        var res = schemaProvider.ExecuteRequestWithContext(gql, new TestSchema4(), null, null);
        Assert.Null(res.Errors);

        Assert.Equal("IUnion", ((dynamic)res.Data!["__type"]!).name);
        Assert.Equal("UNION", ((dynamic)res.Data!["__type"]!).kind);
        Assert.Equal("Person", ((dynamic)res.Data!["__type"]!).possibleTypes[0].name);
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
        var schemaBuilderOptions = new SchemaBuilderOptions { IgnoreTypes = new HashSet<Type> { typeof(C) } };

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
    public void TypeWithIndexer()
    {
        var schemaProvider = SchemaBuilder.FromObject<TestSchema5>();

        var gql = new QueryRequest
        {
            Query = """
                query IntrospectionQuery {
                  __type(name: "Article") {
                    name
                    fields
                    {
                        name
                    }
                  }
                }
                """
        };

        var res = schemaProvider.ExecuteRequestWithContext(gql, new TestSchema5(), null, null);
        Assert.Null(res.Errors);

        dynamic typeDef = res.Data!["__type"]!;
        Assert.Equal("Article", typeDef.name);
        Assert.Collection((IEnumerable<dynamic>)typeDef.fields, item => Assert.Equal("title", item.name), item => Assert.Equal("contents", item.name), item => Assert.Equal("searchVector", item.name));
    }

    [Fact]
    public void TestIgnoreQueryFails()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        // Add a argument field with a require parameter
        var gql = new QueryRequest { Query = @"query Test { movies { id } }", };
        dynamic results = schemaProvider.ExecuteRequestWithContext(gql, new IgnoreTestSchema(), null, null).Errors!;
        var err = Enumerable.First(results);
        Assert.Equal("Field 'movies' not found on type 'Query'", err.Message);
    }

    [Fact]
    public void TestIgnoreQueryPasses()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        // Add a argument field with a require parameter
        var gql = new QueryRequest { Query = @"query Test { albums { id } }", };
        var results = schemaProvider.ExecuteRequestWithContext(gql, new IgnoreTestSchema(), null, null);
        Assert.Empty((IEnumerable)results.Data!["albums"]!);
    }

    [Fact]
    public void TestIgnoreInputFails()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        schemaProvider.AddMutationsFrom<IgnoreTestMutations>();
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"mutation Test($name: String, $hiddenInputField: String) {
                    addAlbum(name: $name, hiddenInputField: $hiddenInputField) {
                        id
                    }
                }",
            Variables = new QueryVariables { { "name", "Balance, Not Symmetry" }, { "hiddenInputField", "yeh" }, }
        };
        var results = schemaProvider.ExecuteRequestWithContext(gql, new IgnoreTestSchema(), null, null);
        var error = results.Errors!.First();
        Assert.Equal("No argument 'hiddenInputField' found on field 'addAlbum'", error.Message);
    }

    [Fact]
    public void TestIgnoreInputPasses()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        schemaProvider.AddMutationsFrom<IgnoreTestMutations>();
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"mutation Test($name: String) {
                    addAlbum(name: $name genre: ""Rock"") {
                        id name hiddenInputField
                    }
                }",
            Variables = new QueryVariables { { "name", "Balance, Not Symmetry" }, }
        };
        var results = schemaProvider.ExecuteRequestWithContext(gql, new IgnoreTestSchema(), null, null);
        Assert.Null(results.Errors);
        dynamic data = results.Data!["addAlbum"]!;
        Assert.Equal("Balance, Not Symmetry", data.name);
        Assert.Null(data.hiddenInputField); // not hidden from query
        Assert.InRange(data.id, 0, 100);
    }

    [Fact]
    public void TestIgnoreAllInInput()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        schemaProvider.AddMutationsFrom<IgnoreTestMutations>();
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"mutation Test($name: String, $hiddenField: String) {
                    addAlbum(name: $name, hiddenField: $hiddenField) {
                        id
                    }
                }",
            Variables = new QueryVariables { { "name", "Balance, Not Symmetry" }, { "hiddenField", "yeh" }, }
        };
        var results = schemaProvider.ExecuteRequestWithContext(gql, new IgnoreTestSchema(), null, null);
        var error = results.Errors!.First();
        Assert.Equal("No argument 'hiddenField' found on field 'addAlbum'", error.Message);
    }

    [Fact]
    public void TestIgnoreAllInQuery()
    {
        var schemaProvider = SchemaBuilder.FromObject<IgnoreTestSchema>();
        // Add a argument field with a require parameter
        var gql = new QueryRequest
        {
            Query =
                @"query Test {
                    albums {
                        id hiddenInputField hiddenField
                    }
                }",
            Variables = new QueryVariables { }
        };
        var results = schemaProvider.ExecuteRequestWithContext(gql, new IgnoreTestSchema(), null, null);
        Assert.NotNull(results.Errors);
        var error = results.Errors.First();
        Assert.Equal("Field 'hiddenField' not found on type 'Album'", error.Message);
    }

    [Fact]
    public void TestIgnoreDataAnnotations()
    {
        var options = new SchemaBuilderOptions { IgnoreAttributes = new HashSet<Type> { typeof(CustomIgnoreAttribute) } };
        var schema = SchemaBuilder.FromObject<TestClassWithIgnoredAnnotation>(options);

        Assert.True(schema.Type<TestClassWithIgnoredAnnotation>().HasField("normalField", null));
        Assert.False(schema.Type<TestClassWithIgnoredAnnotation>().HasField("ignoredField", null));
    }

    private class CustomIgnoreAttribute : Attribute { }

    private class TestClassWithIgnoredAnnotation
    {
        public string NormalField { get; set; } = string.Empty;

        [CustomIgnore]
        public string IgnoredField { get; set; } = string.Empty;
    }

    private class TestIgnoreTypesSchema
    {
        public IEnumerable<A> As { get; } = [];
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
        public TestEntity? SomeRelation { get; }
        public IEnumerable<Person> People { get; } = [];
        public IEnumerable<IdInherited> Projects { get; } = [];
    }

    private class IdInherited : HasId, ISomething { }

    private interface IUnion { }

    private interface ISomething
    {
        string Name { get; }
    }

    private abstract class HasId
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class TestSchema2
    {
        public IEnumerable<Property> Properties { get; } = [];
    }

    private class TestSchema3
    {
        public IEnumerable<AbstractClass> AbstractClasses { get; } = [];
    }

    private class TestSchema4
    {
        public IEnumerable<IUnion> Union { get; } = [];
    }

    private class TestSchema5
    {
        public IEnumerable<Article> Articles { get; } = [];

        public class TsVector
        {
            public char this[int index] => 'a';
        }

        public class Article
        {
            public string Title { get; } = string.Empty;
            public string Contents { get; } = string.Empty;
            public TsVector? SearchVector { get; }
        }
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
        public Person? Relation { get; }
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
