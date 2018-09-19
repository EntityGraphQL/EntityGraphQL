using Xunit;
using System.Collections.Generic;
using System.Linq;
using EntityQueryLanguage.GraphQL.Parsing;
using Microsoft.EntityFrameworkCore;
using static EntityQueryLanguage.ArgumentHelper;
using EntityQueryLanguage.Schema;
using EntityQueryLanguage.Compiler;
using System;
using System.Linq.Expressions;
using System.Collections;

namespace EntityQueryLanguage.GraphQL.Tests
{
    public class RelationHandlerTests
    {
        [Fact]
        public void CalledOnRelations()
        {
            var relationHandler = new TestRelationHandler();
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider(), relationHandler).Compile(@"query {
	People { user { id } }
}");
            Assert.Single(relationHandler.Fields);
            Assert.Equal(typeof(User), relationHandler.Fields.ElementAt(0).Type);
        }
        [Fact]
        public void CalledOnRelationsArray()
        {
            var relationHandler = new TestRelationHandler();
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider(), relationHandler).Compile(@"query {
	People { user { id } projects {id} }
}");
            Assert.Equal(2, relationHandler.Fields.Count);
            Assert.Equal(typeof(User), relationHandler.Fields.ElementAt(0).Type);
            Assert.Equal(typeof(IEnumerable<Project>), relationHandler.Fields.ElementAt(1).Type);
        }

        [Fact]
        public void CalledOnRelationsArrayFromFieldWithArgument()
        {
            var relationHandler = new TestRelationHandler();
            var tree = new GraphQLCompiler(SchemaBuilder.FromObject<TestSchema>(), new DefaultMethodProvider(), relationHandler).Compile(@"{
	person(id: 99) { projects {id} }
}");
            Assert.Equal(1, relationHandler.Fields.Count);
            Assert.Equal(typeof(IEnumerable<Project>), relationHandler.Fields.ElementAt(0).Type);
        }

        private class TestSchema
        {
            public string Hello { get { return "returned value"; } }
            public IEnumerable<Person> People { get { return new List<Person> { new Person() }; } }
            public IEnumerable<User> Users { get { return new List<User> { new User(9), new User(1) }; } }
        }

        private class User
        {

            public User(int id)
            {
                this.Id = id;
            }

            public int Id { get; private set; }
            public int Field1 { get { return 2; } }
            public string Field2 { get { return "2"; } }
            public Person Relation { get { return new Person(); } }
        }

        private class Person
        {
            public int Id { get { return 99; } }
            public string Name { get { return "Luke"; } }
            public string LastName { get { return "Last Name"; } }
            public User User { get { return new User(100); } }
            public double Height { get { return 183.0; } }
            public IEnumerable<Project> Projects { get { return new List<Project> { new Project() }; } }
        }
        private class Project
        {
            public int Id { get { return 55; } }
            public string Name { get { return "Project 3"; } }
        }
    }

    internal class TestRelationHandler : IRelationHandler
    {
        public IList<Expression> Fields { get; private set; } = new List<Expression>();

        public Expression BuildNodeForSelect(List<Expression> relationFields, ParameterExpression contextParameter, Expression exp)
        {
            foreach (var relation in relationFields)
            {
                Fields.Add(relation);
            }
            return exp;
        }

        public Expression HandleSelectComplete(Expression baseExpression)
        {
            return baseExpression;
        }
    }
}