using Xunit;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Tests.GqlCompiling
{
    /// <summary>
    /// Tests directives
    /// </summary>
    public class DirectiveCompileTests
    {
        [Fact]
        public void ParseDirectiveOnField()
        {
            var objectSchemaProvider = SchemaBuilder.FromObject<TestSchema>();
            var tree = new GraphQLCompiler(objectSchemaProvider, new DefaultMethodProvider()).Compile(@"query {
  people { id name @skip(if: false) }
}");
            Assert.Single(tree.Operations);
            Assert.Single(tree.Operations.First().QueryFields);
        }

        [Fact]
        public void ParseDirectiveWithArgs()
        {
            var objectSchemaProvider = SchemaBuilder.FromObject<TestSchema>();
            var tree = new GraphQLCompiler(objectSchemaProvider, new DefaultMethodProvider()).Compile(@"query {
  people {
      id
      name @include(if: true)
    }
}");
            Assert.Single(tree.Operations);
            Assert.Single(tree.Operations.First().QueryFields);
        }
    }
}