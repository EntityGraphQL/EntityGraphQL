using System;
using System.Collections.Generic;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class InputTypeTests
{
    [Fact]
    public void SupportsEnumInInputType_Introspection()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddInputType<PeopleArgs>("PeopleArgs", "people filter args").AddAllFields();
        schema.Query().ReplaceField("people",
            new { args = (PeopleArgs)null },
            (p, param) => p.People, "Return people");
        var gql = new QueryRequest
        {
            Query = @"query {
                        __type(name: ""PeopleArgs"") {
                            name
                            fields {
                                name
                            }
                        }
                    }"
        };
        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data["__type"]).fields, f => f.name == "unit");
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data["__type"]).fields, f => f.name == "dayOfWeek");
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data["__type"]).fields, f => f.name == "name");
    }

    [Fact]
    public void SupportsEnumInInputTypeAsList_Introspection()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddInputType<PeopleArgs>("PeopleArgs", "people filter args").AddAllFields();
        schema.Query().ReplaceField("people",
            new { args = (List<PeopleArgs>)null },
            (p, param) => p.People, "Return people");
        var gql = new QueryRequest
        {
            Query = @"query {
                        __type(name: ""PeopleArgs"") {
                            name
                            fields {
                                name
                            }
                        }
                    }"
        };
        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data["__type"]).fields, f => f.name == "unit");
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data["__type"]).fields, f => f.name == "dayOfWeek");
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data["__type"]).fields, f => f.name == "name");
    }

    [Fact]
    public void SupportsEnumInInputTypeAsListInMutation_Introspection()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        // testing that auto creation of mutation args does correctly add the type
        schema.Mutation().Add("AddPerson", "Description", ([GraphQLInputType] List<PeopleArgs> args) =>
        {
            return true;
        });
        var gql = new QueryRequest
        {
            Query = @"query {
                        __type(name: ""PeopleArgs"") {
                            name
                            fields {
                                name
                            }
                        }
                    }"
        };
        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data["__type"]).fields, f => f.name == "unit");
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data["__type"]).fields, f => f.name == "dayOfWeek");
        Assert.Contains((IEnumerable<dynamic>)((dynamic)result.Data["__type"]).fields, f => f.name == "name");
    }

    internal class PeopleArgs
    {
        // System enum
        public DayOfWeek DayOfWeek { get; set; }
        public HeightUnit Unit { get; set; }
        public string Name { get; set; }
    }
}