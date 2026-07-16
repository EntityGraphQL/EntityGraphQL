using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// schema.Query().AddFieldsFrom&lt;T&gt;() / schema.AddQueryFieldsFrom&lt;T&gt;() - group related field definitions
/// into classes using [GraphQLField] marked methods instead of many AddField() calls
/// </summary>
public class AddFieldsFromTests
{
    private class PeopleQueries : IFieldsFor<TestDataContext>
    {
        [GraphQLField("tallPeople", "People 180cm or taller")]
        public static IEnumerable<Person> TallPeople(TestDataContext db) => db.People.Where(p => p.Height >= 180);

        [GraphQLField]
        public IEnumerable<Person> PeopleByHeight(TestDataContext db, int minHeight) => db.People.Where(p => p.Height >= minHeight);
    }

    public class GreetingService
    {
        public string Greet(string name) => $"Hello {name}";
    }

    private class ServiceQueries : IFieldsFor<TestDataContext>
    {
        [GraphQLField("greeting", "A greeting from a service")]
        public static string Greeting(GreetingService srv) => srv.Greet("Luke");
    }

    private class PersonExtraFields : IFieldsFor<Person>
    {
        [GraphQLField("nameLength", "Length of the person's name")]
        public static int NameLength(Person person) => person.Name.Length;
    }

    private class NoParameterlessCtor : IFieldsFor<TestDataContext>
    {
        private NoParameterlessCtor(int _) { }

        [GraphQLField]
        public int Broken() => 1;
    }

    [Fact]
    public void AddFieldsFrom_StaticMethod_BindsContextAndNoServices()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddFieldsFrom<PeopleQueries>();

        var field = schema.Query().GetField("tallPeople", null);
        Assert.Equal("People 180cm or taller", field.Description);
        // the context parameter binds to the field context - it must NOT be a DI service so the field
        // stays on the database-bound execution pass
        Assert.Empty(field.Services);

        var context = new TestDataContext().FillWithTestData();
        context.People.Add(
            new Person
            {
                Id = 55,
                Name = "Tall",
                Height = 200,
            }
        );
        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ tallPeople { name height } }" }, context, null, null);
        Assert.Null(res.Errors);
        dynamic tallPeople = res.Data!["tallPeople"]!;
        Assert.All((IEnumerable<dynamic>)tallPeople, p => Assert.True(p.height >= 180));
    }

    [Fact]
    public void AddFieldsFrom_InstanceMethod_WithArgs()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddFieldsFrom<PeopleQueries>();

        var context = new TestDataContext().FillWithTestData();
        context.People.Add(
            new Person
            {
                Id = 66,
                Name = "Tall",
                Height = 200,
            }
        );
        // default field naming applied to the method name
        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ peopleByHeight(minHeight: 190) { name } }" }, context, null, null);
        Assert.Null(res.Errors);
        dynamic people = res.Data!["peopleByHeight"]!;
        Assert.Single(people);
        Assert.Equal("Tall", people[0].name);
    }

    [Fact]
    public void AddQueryFieldsFrom_ShortcutWorks()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddQueryFieldsFrom<PeopleQueries>();
        Assert.True(schema.Query().HasField("tallPeople", null));
        Assert.True(schema.Query().HasField("peopleByHeight", null));
    }

    [Fact]
    public void AddFieldsFrom_MethodWithService_ResolvesFromDI()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddQueryFieldsFrom<ServiceQueries>();

        var field = schema.Query().GetField("greeting", null);
        Assert.Single(field.Services);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(new GreetingService());
        serviceCollection.AddSingleton(new TestDataContext());
        var sp = serviceCollection.BuildServiceProvider();

        var res = schema.ExecuteRequest(new QueryRequest { Query = "{ greeting }" }, sp, null);
        Assert.Null(res.Errors);
        Assert.Equal("Hello Luke", res.Data!["greeting"]);
    }

    [Fact]
    public void AddFieldsFrom_OnNonQueryType_BindsThatTypeAsContext()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddFieldsFrom<PersonExtraFields>();

        var context = new TestDataContext().FillWithTestData();
        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { name nameLength } }" }, context, null, null);
        Assert.Null(res.Errors);
        dynamic people = res.Data!["people"]!;
        Assert.Equal(((string)people[0].name).Length, (int)people[0].nameLength);
    }

    [Fact]
    public void AddFieldsFrom_InstanceMethodWithoutParameterlessCtor_ThrowsClearError()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var ex = Assert.Throws<EntityGraphQLSchemaException>(() => schema.Query().AddFieldsFrom<NoParameterlessCtor>());
        Assert.Contains("parameterless constructor", ex.Message);
    }

    [Fact]
    public void AddFieldsFrom_DuplicateFieldName_Throws()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddFieldsFrom<PeopleQueries>();
        var ex = Assert.Throws<EntityGraphQLSchemaException>(() => schema.Query().AddFieldsFrom<PeopleQueries>());
        Assert.Contains("already exists", ex.Message);
    }

    private class DescribedQueries : IFieldsFor<TestDataContext>
    {
        [GraphQLField("shortPeople")]
        [Description("People under 150cm")]
        public static IEnumerable<Person> ShortPeople(TestDataContext db) => db.People.Where(p => p.Height < 150);
    }

    [Fact]
    public void AddFieldsFrom_DescriptionAttribute_IsUsed()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddQueryFieldsFrom<DescribedQueries>();
        var field = schema.Query().GetField("shortPeople", null);
        Assert.Equal("People under 150cm", field.Description);
    }

    private class DateFields : IFieldsFor<DateTime>
    {
        [GraphQLField]
        public static int Year(DateTime d) => d.Year;
    }

    [Fact]
    public void AddFieldsFrom_OnScalarType_Throws()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var scalar = schema.AddScalarType<DateTime>("MyDate", "date");
        var ex = Assert.Throws<EntityGraphQLSchemaException>(() => scalar.AddFieldsFrom<DateFields>());
        Assert.Contains("object or interface", ex.Message);
    }

    private class PersonExpressionFields : IFieldsFor<Person>
    {
        // expression factory - invoked once at schema build time, the expression composes into the main query
        [GraphQLField("nameAndHeight", "Name and height")]
        public static Expression<Func<Person, string>> NameAndHeight() => p => p.Name + " " + p.Height;

        [GraphQLField("greetingFor", "Greeting via a service")]
        public static Expression<Func<Person, GreetingService, string>> GreetingFor() => (p, srv) => srv.Greet(p.Name);
    }

    private class RootExpressionFields : IFieldsFor<TestDataContext>
    {
        [GraphQLField("tallPeopleExpr", "People 180cm or taller")]
        public static Expression<Func<TestDataContext, IEnumerable<Person>>> TallPeople() => db => db.People.Where(p => p.Height >= 180);
    }

    private class BadExpressionFields : IFieldsFor<Person>
    {
        [GraphQLField]
        public static Expression<Func<Person, int>> WithParams(int notAllowed) => p => notAllowed;
    }

    private class WrongContextExpressionFields : IFieldsFor<Person>
    {
        [GraphQLField]
        public static Expression<Func<Task, int>> WrongContext() => t => t.Id;
    }

    [Fact]
    public void AddFieldsFrom_ExpressionMethod_ComposesIntoQuery()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddFieldsFrom<PersonExpressionFields>();

        var field = schema.Type<Person>().GetField("nameAndHeight", null);
        Assert.Equal("Name and height", field.Description);
        // no services - the expression is part of the database-bound pass
        Assert.Empty(field.Services);

        var context = new TestDataContext().FillWithTestData();
        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { name height nameAndHeight } }" }, context, null, null);
        Assert.Null(res.Errors);
        dynamic people = res.Data!["people"]!;
        Assert.Equal($"{people[0].name} {people[0].height}", (string)people[0].nameAndHeight);
    }

    [Fact]
    public void AddFieldsFrom_ExpressionMethodWithService_ResolvesLikeResolveApi()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Type<Person>().AddFieldsFrom<PersonExpressionFields>();

        var field = schema.Type<Person>().GetField("greetingFor", null);
        Assert.Single(field.Services);
        // dependencies are extracted from the visible expression like .Resolve<TService>()
        Assert.NotNull(field.ExtractedFieldsFromServices);

        var context = new TestDataContext().FillWithTestData();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(new GreetingService());
        var sp = serviceCollection.BuildServiceProvider();

        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { name greetingFor } }" }, context, sp, null);
        Assert.Null(res.Errors);
        dynamic people = res.Data!["people"]!;
        Assert.Equal($"Hello {people[0].name}", (string)people[0].greetingFor);
    }

    [Fact]
    public void AddFieldsFrom_ExpressionMethod_OnRootQuery()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddQueryFieldsFrom<RootExpressionFields>();

        var context = new TestDataContext().FillWithTestData();
        context.People.Add(
            new Person
            {
                Id = 77,
                Name = "Tall",
                Height = 200,
            }
        );
        var res = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ tallPeopleExpr { name height } }" }, context, null, null);
        Assert.Null(res.Errors);
        dynamic people = res.Data!["tallPeopleExpr"]!;
        Assert.All((IEnumerable<dynamic>)people, p => Assert.True(p.height >= 180));
    }

    [Fact]
    public void AddFieldsFrom_ExpressionMethodWithParameters_ThrowsClearError()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var ex = Assert.Throws<EntityGraphQLSchemaException>(() => schema.Type<Person>().AddFieldsFrom<BadExpressionFields>());
        Assert.Contains("must not take parameters", ex.Message);
    }

    [Fact]
    public void AddFieldsFrom_ExpressionMethodWrongContextType_ThrowsClearError()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var ex = Assert.Throws<EntityGraphQLSchemaException>(() => schema.Type<Person>().AddFieldsFrom<WrongContextExpressionFields>());
        Assert.Contains("first parameter must be the context type", ex.Message);
    }
}
