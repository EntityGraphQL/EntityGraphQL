using Xunit;
using System.Collections.Generic;
using EntityGraphQL.Schema;
using System.Linq;

namespace EntityGraphQL.Tests
{
    public class IntrospectionTests
    {
        [Fact]
        public void TestGraphiQLIntrospection()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            var gql = new QueryRequest
            {
                Query = @"
query IntrospectionQuery {
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
}
"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>()
            };

            var res = schema.ExecuteRequest(gql, context, null, null);
            Assert.Null(res.Errors);
        }

        [Fact]
        public void TestGraphiQLIntrospectionFragInFrag()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            var gql = new QueryRequest
            {
                Query = @"
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
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>()
            };

            var res = schema.ExecuteRequest(gql, context, null, null);
            Assert.Null(res.Errors);
        }

        [Fact]
        public void TestDeprecateMethod()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            schema.UpdateType<Project>(t =>
            {
                t.GetField(p => p.Owner).Deprecate("This is deprecated");
            });

            var gql = new QueryRequest
            {
                Query = @"
        query IntrospectionQuery {
          __type(name: ""Project"") {
            fields {
              name
              isDeprecated
              deprecationReason
            }
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>()
            };

            var res = schema.ExecuteRequest(gql, context, null, null);
            Assert.Null(res.Errors);
            var fields = (IEnumerable<dynamic>)((dynamic)res.Data["__type"]).fields;
            Assert.True(Enumerable.Any(fields));
            Assert.DoesNotContain(fields, f => f.name == "owner");
        }

        [Fact]
        public void TestObsoleteAttribute()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>();

            var gql = new QueryRequest
            {
                Query = @"
        query IntrospectionQuery {
          __type(name: ""Query"") {
            fields {
              name
              isDeprecated
              deprecationReason
            }
          }
        }"
            };

            var context = new TestDataContext
            {
                Projects = new List<Project>()
            };

            var res = schema.ExecuteRequest(gql, context, null, null);
            Assert.Null(res.Errors);
            var fields = (IEnumerable<dynamic>)((dynamic)res.Data["__type"]).fields;
            Assert.True(Enumerable.Any(fields));
            Assert.DoesNotContain(fields, f => f.name == "projectsOld");
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
    }
}