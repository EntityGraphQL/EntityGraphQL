using System.Collections.Generic;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static EntityGraphQL.Tests.ServiceFieldTests;

namespace EntityGraphQL.Tests;

public class GraphQLFieldAttributeTests
{
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
                     query TypeWithMethod($value: Int!) {
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
    public void TestGraphQLFieldAttributeWithOptionalArgs()
    {
        var schemaProvider = SchemaBuilder.FromObject<ContextFieldWithMethod>();

        Assert.True(schemaProvider.HasType(typeof(TypeWithMethod)));

        Assert.True(schemaProvider.Type<TypeWithMethod>().HasField("methodFieldWithOptionalArgs", null));

        var sdl = schemaProvider.ToGraphQLSchemaString();

        Assert.Contains("fields: [TypeWithMethod!]", sdl);
        Assert.Contains("methodFieldWithOptionalArgs(value: String): Int!", sdl);

        var gql = new QueryRequest
        {
            Query = @"
                     query TypeWithMethod {
                       fields {
                         methodFieldWithOptionalArgs
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

        Assert.Equal(20, ((dynamic)res.Data["fields"])[0].methodFieldWithOptionalArgs);
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

        Assert.Equal(23, (dynamic)res.Data["testMethod"]);
    }

    [Fact]
    public void TestGraphQLFieldAttributeStatic()
    {
        var schemaProvider = SchemaBuilder.FromObject<ContextFieldWithMethod>();

        Assert.True(schemaProvider.Type<TypeWithMethod>().HasField("staticMethodField", null));

        var sdl = schemaProvider.ToGraphQLSchemaString();

        Assert.Contains("fields: [TypeWithMethod!]", sdl);
        Assert.Contains("staticMethodField(value: Int!): Int!", sdl);

        var gql = new QueryRequest
        {
            Query = @"query TypeWithMethod {
                fields {
                    staticMethodField(value: 88)
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

        Assert.Equal(88, ((dynamic)res.Data["fields"])[0].staticMethodField);
    }

    [Fact]
    public void TestGraphQLFieldAttributeWithService()
    {
        var schemaProvider = SchemaBuilder.FromObject<ContextFieldWithMethodService>();

        Assert.True(schemaProvider.Type<ContextFieldWithMethodService>().HasField("methodFieldWithService", null));

        var sdl = schemaProvider.ToGraphQLSchemaString();

        Assert.Contains("methodFieldWithService(value: Int!): Int!", sdl);

        var gql = new QueryRequest
        {
            Query = @"query TypeWithMethod {
                methodFieldWithService(value: 88)
            }"
        };

        var context = new ContextFieldWithMethodService();
        var serviceCollection = new ServiceCollection();
        var srv = new ConfigService();
        serviceCollection.AddSingleton(srv);

        var res = schemaProvider.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
        Assert.Null(res.Errors);
        Assert.Equal(44, (dynamic)res.Data["methodFieldWithService"]);
    }

    [Fact]
    public void TestGraphQLFieldAttributeWithServiceStatic()
    {
        var schemaProvider = SchemaBuilder.FromObject<ContextFieldWithMethodService>();

        Assert.True(schemaProvider.Type<ContextFieldWithMethodService>().HasField("methodFieldWithServiceStatic", null));

        var sdl = schemaProvider.ToGraphQLSchemaString();

        Assert.Contains("methodFieldWithServiceStatic(value: Int!): Int!", sdl);

        var gql = new QueryRequest
        {
            Query = @"query TypeWithMethod {
                methodFieldWithServiceStatic(value: 88)
            }"
        };

        var context = new ContextFieldWithMethodService();
        var serviceCollection = new ServiceCollection();
        var srv = new ConfigService();
        serviceCollection.AddSingleton(srv);

        var res = schemaProvider.ExecuteRequest(gql, context, serviceCollection.BuildServiceProvider(), null);
        Assert.Null(res.Errors);
        Assert.Equal(44, (dynamic)res.Data["methodFieldWithServiceStatic"]);
    }
}


public class ContextFieldWithMethod
{
    public IEnumerable<TypeWithMethod> Fields { get; set; }

    [GraphQLField]
    public int TestMethod()
    {
        return 23;
    }
}
public class ContextFieldWithMethodService
{
    [GraphQLField]
    public int MethodFieldWithService(ConfigService service, int value)
    {
        return service.GetHalfInt(value);
    }
    [GraphQLField]
    public static int MethodFieldWithServiceStatic(ConfigService service, int value)
    {
        return service.GetHalfInt(value);
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
    public static int StaticMethodField(int value)
    {
        return value;
    }

    [GraphQLField]
    public int methodFieldWithOptionalArgs(string value)
    {
        return 20;
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