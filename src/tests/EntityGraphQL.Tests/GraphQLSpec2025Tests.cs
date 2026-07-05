using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// Tests for behaviors added/clarified in the September 2025 GraphQL specification
/// </summary>
public class GraphQLSpec2025Tests
{
    [Fact]
    public void NonRepeatableDirective_UsedTwiceAtOneLocation_IsError()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ people @skip(if: false) @skip(if: false) { id } }" },
            new TestDataContext(),
            null,
            null
        );

        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors!, e => e.Message.Contains("can only be used once"));
    }

    [Fact]
    public void SkipAndIncludeTogether_IsNotADuplicate()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people @skip(if: false) @include(if: true) { id } }" }, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
    }

    [Fact]
    public void SkipOrInclude_OnSubscriptionRootField_IsError()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddType<SubscriptionTests.Message>("Message info").AddAllFields();
        schema.Subscription().AddFrom<SubscriptionTests.TestSubscriptions>();

        var ex = Assert.ThrowsAny<Exception>(() => GraphQLParser.Parse("subscription { onMessage @skip(if: false) { id } }", schema));
        Assert.Contains("not allowed on the root field of a subscription", ex.Message);

        var ex2 = Assert.ThrowsAny<Exception>(() => GraphQLParser.Parse("subscription { onMessage @include(if: true) { id } }", schema));
        Assert.Contains("not allowed on the root field of a subscription", ex2.Message);
    }

    [Fact]
    public void Introspection_DirectiveIsRepeatable_IsQueryable()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ __schema { directives { name isRepeatable } } }" }, new TestDataContext(), null, null);

        Assert.Null(result.Errors);
        var directives = ((IEnumerable<dynamic>)((dynamic)result.Data!["__schema"]!).directives).ToList();
        Assert.Contains(directives, d => d.name == "skip");
        // all built-in directives are non-repeatable
        Assert.All(directives, d => Assert.False(d.isRepeatable));
    }

    private class ArgsWithDeprecated
    {
        public int? Limit { get; set; }

        [Obsolete("Use limit instead")]
        public int? Take { get; set; }
    }

    [Fact]
    public void Introspection_DeprecatedArgument_HiddenByDefault_VisibleWithIncludeDeprecated()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("pagedPeople", new ArgsWithDeprecated(), (ctx, args) => ctx.People, "People with paging args");

        var query =
            @"{
                __type(name: ""Query"") {
                    fields {
                        name
                        args { name }
                        allArgs: args(includeDeprecated: true) { name isDeprecated deprecationReason }
                    }
                }
            }";
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, new TestDataContext(), null, null);

        Assert.Null(result.Errors);
        var fields = ((IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).fields).ToList();
        var field = fields.First(f => f.name == "pagedPeople");

        var defaultArgs = ((IEnumerable<dynamic>)field.args).Select(a => (string)a.name).ToList();
        Assert.Contains("limit", defaultArgs);
        Assert.DoesNotContain("take", defaultArgs);

        var allArgs = ((IEnumerable<dynamic>)field.allArgs).ToList();
        var take = allArgs.First(a => a.name == "take");
        Assert.True(take.isDeprecated);
        Assert.Equal("Use limit instead", take.deprecationReason);
    }

    private class RequiredDeprecatedArgs
    {
        [Obsolete("nope")]
        public RequiredField<int> Required { get; set; } = new();
    }

    [Fact]
    public void DeprecatedRequiredArgument_IsSchemaError()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var ex = Assert.Throws<EntityGraphQLSchemaException>(() => schema.Query().AddField("bad", new RequiredDeprecatedArgs(), (ctx, args) => ctx.People, "Bad args"));
        Assert.Contains("can not be deprecated as it is required", ex.Message);
    }

    private class FilterInput
    {
        public string? Name { get; set; }
        public string? OldName { get; set; }
    }

    [Fact]
    public void Introspection_DeprecatedInputField_HiddenByDefault_VisibleWithIncludeDeprecated()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var inputType = schema.AddInputType<FilterInput>("FilterInput", "A filter").AddAllFields();
        inputType.GetField("oldName", null).Deprecate("Use name instead");

        var query =
            @"{
                __type(name: ""FilterInput"") {
                    inputFields { name }
                    allInputFields: inputFields(includeDeprecated: true) { name isDeprecated deprecationReason }
                }
            }";
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, new TestDataContext(), null, null);

        Assert.Null(result.Errors);
        dynamic type = result.Data!["__type"]!;
        var defaultFields = ((IEnumerable<dynamic>)type.inputFields).Select(f => (string)f.name).ToList();
        Assert.Contains("name", defaultFields);
        Assert.DoesNotContain("oldName", defaultFields);

        var allFields = ((IEnumerable<dynamic>)type.allInputFields).ToList();
        var oldName = allFields.First(f => f.name == "oldName");
        Assert.True(oldName.isDeprecated);
        Assert.Equal("Use name instead", oldName.deprecationReason);
    }

    private interface IThing
    {
        string Name { get; }
    }

    private class GoodThing : IThing
    {
        public string Name { get; set; } = "";
    }

    private class BadThing
    {
        public int Name { get; set; }
    }

    [Fact]
    public void Implements_IncompatibleFieldType_IsSchemaError()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddInterface<IThing>("Thing", "A thing").AddAllFields();
        var bad = schema.AddType<BadThing>("BadThing", "Bad").AddAllFields();

        // BadThing.name is Int - not covariant with the interface's String name
        var ex = Assert.Throws<EntityGraphQLSchemaException>(() => bad.Implements<IThing>());
        Assert.Contains("not compatible with the interface's field type", ex.Message);
    }

    [Fact]
    public void Implements_CompatibleFieldType_IsAllowed()
    {
        var schema = new SchemaProvider<TestDataContext>();
        schema.AddInterface<IThing>("Thing", "A thing").AddAllFields();
        var good = schema.AddType<GoodThing>("GoodThing", "Good").AddAllFields();
        good.Implements<IThing>();

        Assert.Contains(good.BaseTypesReadOnly, t => t.Name == "Thing");
    }

    [Fact]
    public void FieldMerging_SameAliasDifferentFields_IsError()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        data.People.Add(new Person { Id = 1, Name = "A" });

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { x: id x: name } }" }, data, null, null);

        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors!, e => e.Message.Contains("must be the same field with identical arguments"));
    }

    [Fact]
    public void FieldMerging_SameFieldDifferentArguments_IsError()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ project(id: 1) { name } project(id: 2) { name } }" }, new TestDataContext(), null, null);

        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors!, e => e.Message.Contains("must be the same field with identical arguments"));
    }

    [Fact]
    public void FieldMerging_IdenticalDuplicateField_IsAllowed()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        data.People.Add(new Person { Id = 1, Name = "A" });

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { id id name } }" }, data, null, null);
        Assert.Null(result.Errors);
    }

    [Fact]
    public void FieldMerging_ExclusiveFragments_IncompatibleShapes_IsError()
    {
        var schema = new ApiVersion1.TestAbstractDataGraphSchema();
        var context = new TestAbstractDataContext();
        context.Animals.Add(new Cat { Name = "Felix", Lives = 9 });

        // Cat.lives is Int!, Dog.name is String! - same response name, incompatible shapes
        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ animals { ... on Cat { value: lives } ... on Dog { value: name } } }" },
            context,
            null,
            null
        );

        Assert.NotNull(result.Errors);
        Assert.Contains(result.Errors!, e => e.Message.Contains("must have compatible types"));
    }

    [Fact]
    public void FieldMerging_ExclusiveFragments_CompatibleShapes_IsAllowed()
    {
        var schema = new ApiVersion1.TestAbstractDataGraphSchema();
        var context = new TestAbstractDataContext();
        context.Animals.Add(new Cat { Name = "Felix", Lives = 9 });
        context.Animals.Add(new Dog { Name = "Rex", HasBone = true });

        // both Int! - mergeable shape
        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ animals { ... on Cat { value: lives } ... on Dog { value: id } } }" },
            context,
            null,
            null
        );

        Assert.Null(result.Errors);
    }

    private class CoercionArgs
    {
        public double Ratio { get; set; } = 1.5;
        public List<int> Ids { get; set; } = [2];
    }

    [Fact]
    public void DefaultValueCoercion_IntLiteralForFloatArg_AndOmittedDefaults()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("coerce", new CoercionArgs(), (ctx, args) => args.Ratio, "Coercion test");

        // omitted args use the defaults
        var r1 = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ coerce }" }, new TestDataContext(), null, null);
        Assert.Null(r1.Errors);
        Assert.Equal(1.5, r1.Data!["coerce"]);

        // an Int literal coerces to Float per the spec coercion rules
        var r2 = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ coerce(ratio: 3) }" }, new TestDataContext(), null, null);
        Assert.Null(r2.Errors);
        Assert.Equal(3.0, r2.Data!["coerce"]);
    }

    [Fact]
    public void Sdl_DeprecatedArgument_IsAnnotated()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("pagedPeople", new ArgsWithDeprecated(), (ctx, args) => ctx.People, "People with paging args");

        var sdl = schema.ToGraphQLSchemaString();
        Assert.Contains("take: Int @deprecated(reason: \"Use limit instead\")", sdl);
    }
}
