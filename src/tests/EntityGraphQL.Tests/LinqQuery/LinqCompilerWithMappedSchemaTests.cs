using Xunit;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Tests.ApiVersion1;
using System;
using EntityGraphQL.Tests;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.LinqQuery.Tests
{
    /// Tests that our compiler correctly compiles all the basic parts of our language against a given schema provider
    public class LinqCompilerWithMappedSchemaTests
    {
        [Fact]
        public void TestConversionToGuid()
        {
            var exp = EqlCompiler.Compile("people.where(guid = \"6492f5fe-0869-4279-88df-7f82f8e87a67\")", new TestObjectGraphSchema());
            dynamic result = exp.Execute(GetDataContext());
            Assert.Equal(1, Enumerable.Count(result));
        }

        [Fact]
        public void CompilesIdentityCall()
        {
            var exp = EqlCompiler.Compile("people", new TestObjectGraphSchema());
            dynamic result = exp.Execute(GetDataContext());
            Assert.Equal(1, Enumerable.Count(result));
        }
        [Fact]
        public void CompilesIdentityCallFullPath()
        {
            var schema = new TestObjectGraphSchema();
            var exp = EqlCompiler.Compile("privateProjects.where(id = 8).count()", schema);
            Assert.Equal(0, exp.Execute(GetDataContext()));
            var exp2 = EqlCompiler.Compile("privateProjects.count()", schema);
            Assert.Equal(1, exp2.Execute(GetDataContext()));
        }
        [Fact]
        public void CompilesTypeBuiltFromObject()
        {
            // no brackets so it reads it as someRelation.relation.id = (99 ? 'wooh' : 66) and fails as 99 is not a bool
            var exp = EqlCompiler.Compile("defaultlocation.id = 10", new TestObjectGraphSchema());
            Assert.True((bool)exp.Execute(GetDataContext()));
        }
        [Fact]
        public void CompilesIfThenElseInlineFalseBrackets()
        {
            var exp = EqlCompiler.Compile("(publicProjects.Count(id = 90) = 1) ? \"Yes\" : \"No\"", new TestObjectGraphSchema());
            Assert.Equal("Yes", exp.Execute(GetDataContext()));
        }
        [Fact]
        public void CompilesIfThenElseTrue()
        {
            var exp = EqlCompiler.Compile("if publicProjects.Count() > 1 then \"Yes\" else \"No\"", new TestObjectGraphSchema());
            Assert.Equal("No", exp.Execute(GetDataContext()));
        }

        private TestDataContext GetDataContext()
        {
            var db = new TestDataContext();
            db.Projects = new List<Project> { new Project { Id = 90, Type = 2 }, new Project { Id = 91, Type = 1 } };
            db.People = new List<Person> { new Person { Id = 4, Guid = new Guid("6492f5fe-0869-4279-88df-7f82f8e87a67") } };
            db.Locations = new List<Location> { new Location { Id = 10 } };
            return db;
        }
    }
}