using Xunit;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using EntityGraphQL.Schema;
using EntityGraphQL.Compiler;
using System.Collections;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// Tests graphql metadata
    /// </summary>
    public class MutationTests
    {
        [Fact]
        public void MissingRequiredVar()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest {
                Query = @"mutation AddPerson($name: String!) {
  addPerson(name: $name) { id name }
}",
                Variables = new QueryVariables{{"na", "Frank"}}
            };
            dynamic addPersonResult = new TestSchema().QueryObject(gql, schemaProvider).Errors;
            var err = Enumerable.First(addPersonResult);
            Assert.Equal("Missing required variable 'name' on query 'AddPerson'", err.Message);
        }

        [Fact]
        public void SupportsMutationOptional()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest {
                Query = @"mutation AddPerson($name: String) {
  addPerson(name: $name) {
    id name
  }
}",
                Variables = new QueryVariables {}
            };
            dynamic addPersonResult = new TestSchema().QueryObject(gql, schemaProvider).Data;
            addPersonResult = Enumerable.First(addPersonResult);
            addPersonResult = addPersonResult.Value;
            // we only have the fields requested
            Assert.Equal(2, addPersonResult.GetType().GetFields().Length);
            Assert.Equal("Id", addPersonResult.GetType().GetFields()[0].Name);
            Assert.Equal(555, addPersonResult.Id);
            Assert.Equal("Name", addPersonResult.GetType().GetFields()[1].Name);
            Assert.Equal("Default", addPersonResult.Name);
        }

        [Fact]
        public void SupportsMutationArray()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest {
                Query = @"mutation AddPerson($names: [String]) {
  addPersonNames(names: $names) {
    id name lastName
  }
}",
                Variables = new QueryVariables {
                    {"names", new [] {"Bill", "Frank"}}
                }
            };
            dynamic addPersonResult = new TestSchema().QueryObject(gql, schemaProvider).Data;
            addPersonResult = Enumerable.First(addPersonResult);
            addPersonResult = addPersonResult.Value;
            // we only have the fields requested
            Assert.Equal(3, addPersonResult.GetType().GetFields().Length);
            Assert.Equal("Id", addPersonResult.GetType().GetFields()[0].Name);
            Assert.Equal(0, addPersonResult.Id);
            Assert.Equal("Name", addPersonResult.GetType().GetFields()[1].Name);
            Assert.Equal("Bill", addPersonResult.Name);
            Assert.Equal("Frank", addPersonResult.LastName);
        }

        [Fact]
        public void SupportsMutationObject()
        {
            var schemaProvider = SchemaBuilder.FromObject<TestSchema>(false);
            schemaProvider.AddMutationFrom(new PeopleMutations());
            // Add a argument field with a require parameter
            var gql = new QueryRequest {
                Query = @"mutation AddPerson($names: [String]) {
  addPersonInput(nameInput: $names) {
    id name lastName
  }
}",
                // object will come through as json in the request
                Variables = new QueryVariables {
                    {"names", Newtonsoft.Json.JsonConvert.DeserializeObject("{\"name\": \"Lisa\", \"lastName\": \"Simpson\"}")}
                }
            };
            dynamic addPersonResult = new TestSchema().QueryObject(gql, schemaProvider);
            Assert.Empty(addPersonResult.Errors);
            addPersonResult = Enumerable.First(addPersonResult.Data);
            addPersonResult = addPersonResult.Value;
            // we only have the fields requested
            Assert.Equal(3, addPersonResult.GetType().GetFields().Length);
            Assert.Equal("Id", addPersonResult.GetType().GetFields()[0].Name);
            Assert.Equal(0, addPersonResult.Id);
            Assert.Equal("Name", addPersonResult.GetType().GetFields()[1].Name);
            Assert.Equal("Lisa", addPersonResult.Name);
            Assert.Equal("Simpson", addPersonResult.LastName);
        }
    }

    internal class TestSchema
    {
        public string Hello { get { return "returned value"; } }
        public IEnumerable<Person> People { get { return new List<Person> { new Person() }; } }
        public IEnumerable<User> Users { get { return new List<User> { new User() }; } }
    }

    internal class DbTestSchema
    {
        public string Hello { get { return "returned value"; } }
        public DbSet<Person> People { get; }
        public DbSet<User> Users { get; }
    }


    internal class User
    {
        public int Id { get { return 100; } }
        public int Field1 { get { return 2; } }
        public string Field2 { get { return "2"; } }
        public Person Relation { get { return new Person(); } }
        public Task NestedRelation { get { return new Task(); } }
    }

    internal class PeopleMutations
    {
        [GraphQLMutation]
        public Person AddPerson(TestSchema db, PeopleMutationsArgs args)
        {
            return new Person { Name = string.IsNullOrEmpty(args.Name) ? "Default" : args.Name, Id = 555 };
        }

        [GraphQLMutation]
        public Person AddPersonNames(TestSchema db, PeopleMutationsArgs args)
        {
            return new Person { Name = args.Names[0], LastName = args.Names[1] };
        }

        [GraphQLMutation]
        public Person AddPersonInput(TestSchema db, PeopleMutationsArgs args)
        {
            return new Person { Name = args.NameInput.Name, LastName = args.NameInput.LastName };
        }
    }

    internal class PeopleMutationsArgs
    {
        public string Name { get; set; }
        public List<string> Names { get; set; }

        public InputObject NameInput { get; set; }
    }

    public class InputObject
    {
        public string Name { get; set; }
        public string LastName { get; set; }
    }
}