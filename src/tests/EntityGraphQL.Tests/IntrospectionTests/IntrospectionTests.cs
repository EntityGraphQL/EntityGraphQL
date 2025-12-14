using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class IntrospectionTests
{
    [Fact]
    public void IncludeEnumInputField_Introspection()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        schema.AddInputType<EnumInputArgs>("EnumInputArgs", "args with enums").AddAllFields();

        var gql = new QueryRequest
        {
            Query =
                @"query {
                        __type(name: ""EnumInputArgs"") {
                            name
                            inputFields {
                                name
                                type { kind name ofType { kind name } }
                            }
                        }
                    }",
        };

        var result = schema.ExecuteRequestWithContext(gql, new TestDataContext(), null, null);
        Assert.Null(result.Errors);

        var fields = (IEnumerable<dynamic>)((dynamic)result.Data!["__type"]!).inputFields;
        Assert.Contains(fields, f => f.name == "unit");

        var unitField = fields.First(f => f.name == "unit");
        Assert.Equal("NON_NULL", unitField.type.kind);
        Assert.Equal("ENUM", unitField.type.ofType.kind);
        Assert.Equal("HeightUnit", unitField.type.ofType.name);
    }

    private class EnumInputArgs
    {
        public HeightUnit Unit { get; set; }
        public DayOfWeek Day { get; set; }
    }

    [Fact]
    public void TestGraphiQLIntrospection()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        var gql = new QueryRequest
        {
            Query =
                @"query IntrospectionQuery {
                  __schema {
                    queryType {
                      name
                    }
                    mutationType {
                      name
                    }
                    subscriptionType {
                      name
                    }
                    types {
                      ...FullType
                    }
                    directives {
                      name
                      description
                      locations
                      args {
                        ...InputValue
                      }
                    }
                  }
                }

                fragment FullType on __Type {
                  name
                  kind
                  description
                  fields(includeDeprecated: true) {
                    name
                    description
                    args {
                      ...InputValue
                    }
                    type {
                      ...TypeRef
                    }
                    isDeprecated
                    deprecationReason
                  }
                  inputFields {
                    ...InputValue
                  }
                  interfaces {
                    ...TypeRef
                  }
                  enumValues(includeDeprecated: true) {
                    name
                    description
                    isDeprecated
                    deprecationReason
                  }
                  possibleTypes {
                    ...TypeRef
                  }
                }

                fragment InputValue on __InputValue {
                  name
                  description
                  type {
                    ...TypeRef
                  }
                  defaultValue
                }

                fragment TypeRef on __Type {
                  name
                  kind
                  ofType {
                    name
                    kind
                    ofType {
                      name
                      kind
                      ofType {
                        name
                        kind
                        ofType {
                          name
                          kind
                          ofType {
                            name
                            kind
                            ofType {
                              name
                              kind
                              ofType {
                                name
                                kind
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }",
        };

        var context = new TestDataContext { Projects = [] };

        var res = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(res.Errors);
    }

    [Fact]
    public void TestGraphiQLIntrospectionFragInFrag()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        var gql = new QueryRequest
        {
            Query =
                @"
        query IntrospectionQuery {
          __schema {
            directives {
              args {
                ...InputValue
              }
            }
          }
        }

        fragment InputValue on __InputValue {
          type {
            ...TypeRef
          }
        }

        fragment TypeRef on __Type {
          name
        }",
        };

        var context = new TestDataContext { Projects = [] };

        var res = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(res.Errors);
    }

    [Fact]
    public void TestDeprecateMethod()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        schema.UpdateType<Project>(t =>
        {
            t.GetField(p => p.Owner!).Deprecate("This is deprecated");
        });

        var gql = new QueryRequest
        {
            Query =
                @"
        query IntrospectionQuery {
          __type(name: ""Project"") {
            fields {
              name
              isDeprecated
              deprecationReason
            }
          }
        }",
        };

        var context = new TestDataContext { Projects = [] };

        var res = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(res.Errors);
        var fields = (IEnumerable<dynamic>)((dynamic)res.Data!["__type"]!).fields;
        Assert.True(Enumerable.Any(fields));
        Assert.DoesNotContain(fields, f => f.name == "owner");

        var sdl = schema.ToGraphQLSchemaString();
        Assert.Contains("owner: Person @deprecated(reason: \"This is deprecated\")", sdl);
    }

    [Fact]
    public void TestObsoleteAttribute()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        var gql = new QueryRequest
        {
            Query =
                @"
        query IntrospectionQuery {
          __type(name: ""Query"") {
            fields {
              name
              isDeprecated
              deprecationReason
            }
          }
        }",
        };

        var context = new TestDataContext { Projects = [] };

        var res = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(res.Errors);
        var fields = (IEnumerable<dynamic>)((dynamic)res.Data!["__type"]!).fields;
        Assert.True(Enumerable.Any(fields));
        Assert.DoesNotContain(fields, f => f.name == "projectsOld");

        var sdl = schema.ToGraphQLSchemaString();
        Assert.Contains("projectsOld: [ProjectOld!]! @deprecated(reason: \"This is obsolete, use Projects instead\")", sdl);
    }

    [Fact]
    public void TestIntrospectionTypesRegistered()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        Assert.True(schema.HasType("__Type"));
        Assert.True(schema.HasType("__EnumValue"));
        Assert.True(schema.HasType("__InputValue"));

        Assert.False(schema.HasType("Type"));
        Assert.False(schema.HasType("EnumValue"));
        Assert.False(schema.HasType("InputValue"));
    }

    [Fact]
    public void TestScalarDescription()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        var gql = new QueryRequest
        {
            Query =
                @"query {
                    __type(name: ""DateTime"") {
                        name
                        description
                    }
                }",
        };

        var context = new TestDataContext();

        var res = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(res.Errors);
        var type = (dynamic)res.Data!["__type"]!;
        Assert.Equal("Date with time scalar", type.description);
    }

    [Fact]
    public void TestInputTypeShouldNotHaveFields_OnlyInputFields()
    {
        // According to GraphQL spec, INPUT_OBJECT types should have inputFields, not fields
        // fields should return null for input types
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddInputType<TestInputType>("TestInputType", "A test input type").AddAllFields();

        var gql = new QueryRequest
        {
            Query =
                @"query {
                    __type(name: ""TestInputType"") {
                        name
                        kind
                        fields {
                            name
                        }
                        inputFields {
                            name
                        }
                    }
                }",
        };

        var context = new TestDataContext();
        var res = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(res.Errors);

        var type = (dynamic)res.Data!["__type"]!;
        Assert.Equal("TestInputType", type.name);
        Assert.Equal("INPUT_OBJECT", type.kind);

        // fields should be null for INPUT_OBJECT types
        Assert.Null(type.fields);

        // inputFields should have the fields
        var inputFields = (IEnumerable<dynamic>)type.inputFields;
        Assert.NotEmpty(inputFields);
        Assert.Contains(inputFields, f => f.name == "name");
        Assert.Contains(inputFields, f => f.name == "value");
    }

    [Fact]
    public void TestObjectTypeShouldHaveFields_NotInputFields()
    {
        // According to GraphQL spec, OBJECT types should have fields, not inputFields
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        var gql = new QueryRequest
        {
            Query =
                @"query {
                    __type(name: ""Person"") {
                        name
                        kind
                        fields {
                            name
                        }
                        inputFields {
                            name
                        }
                    }
                }",
        };

        var context = new TestDataContext();
        var res = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(res.Errors);

        var type = (dynamic)res.Data!["__type"]!;
        Assert.Equal("Person", type.name);
        Assert.Equal("OBJECT", type.kind);

        // fields should have the fields for OBJECT types
        var fields = (IEnumerable<dynamic>)type.fields;
        Assert.NotEmpty(fields);

        // inputFields should be null/empty for OBJECT types
        var inputFields = type.inputFields as IEnumerable<dynamic>;
        Assert.True(inputFields == null || !inputFields.Any());
    }

    private class TestInputType
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
