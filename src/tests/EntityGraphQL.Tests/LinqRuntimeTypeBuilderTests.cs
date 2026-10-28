using System;
using System.Collections.Generic;
using EntityGraphQL.Compiler.Util;
using Xunit;

namespace EntityGraphQL.Tests
{
    public class LinqRuntimeTypeBuilderTests
    {
        [Fact]
        public void SameFields_ReturnsCachedType()
        {
            var fields = new Dictionary<string, Type> { { "id", typeof(int) }, { "name", typeof(string) } };
            var type1 = LinqRuntimeTypeBuilder.Shared.GetDynamicType(fields, "test");
            var type2 = LinqRuntimeTypeBuilder.Shared.GetDynamicType(new Dictionary<string, Type> { { "name", typeof(string) }, { "id", typeof(int) } }, "test");
            Assert.Same(type1, type2);
        }

        [Fact]
        public void DifferentFieldTypes_ReturnsDifferentTypes()
        {
            var type1 = LinqRuntimeTypeBuilder.Shared.GetDynamicType(new Dictionary<string, Type> { { "value", typeof(int) } }, "test");
            var type2 = LinqRuntimeTypeBuilder.Shared.GetDynamicType(new Dictionary<string, Type> { { "value", typeof(long) } }, "test");
            Assert.NotSame(type1, type2);
            Assert.Equal(typeof(int), type1.GetField("value")!.FieldType);
            Assert.Equal(typeof(long), type2.GetField("value")!.FieldType);
        }

        [Fact]
        public void SameFields_ParentTypesWithSameNameDifferentNamespace_ReturnsDifferentTypes()
        {
            // regression: the cache key only included parentType.Name, so two parent types sharing a name
            // (different namespaces) collided and the second caller got a type inheriting the wrong parent
            var fields = new Dictionary<string, Type> { { "extra", typeof(int) } };
            var type1 = LinqRuntimeTypeBuilder.Shared.GetDynamicType(fields, "test", typeof(NamespaceOne.SharedName));
            var type2 = LinqRuntimeTypeBuilder.Shared.GetDynamicType(fields, "test", typeof(NamespaceTwo.SharedName));

            Assert.NotSame(type1, type2);
            Assert.Equal(typeof(NamespaceOne.SharedName), type1.BaseType);
            Assert.Equal(typeof(NamespaceTwo.SharedName), type2.BaseType);
        }

        [Fact]
        public void EachSchema_HasItsOwnCollectibleTypes()
        {
            var fields = new Dictionary<string, Type> { { "id", typeof(int) } };
            var schema = new Schema.SchemaProvider<object>();
            var type1 = LinqRuntimeTypeBuilder.For(schema).GetDynamicType(fields, "test");

            Assert.Same(type1, LinqRuntimeTypeBuilder.For(schema).GetDynamicType(fields, "test"));
            Assert.NotSame(type1, LinqRuntimeTypeBuilder.For(new Schema.SchemaProvider<object>()).GetDynamicType(fields, "test"));
            Assert.True(type1.IsCollectible);
            Assert.Same(LinqRuntimeTypeBuilder.For(schema), LinqRuntimeTypeBuilder.For(type1));
        }

        [Fact]
        public void SameFieldsWithAndWithoutParent_ReturnsDifferentTypes()
        {
            var fields = new Dictionary<string, Type> { { "extra", typeof(int) } };
            var withParent = LinqRuntimeTypeBuilder.Shared.GetDynamicType(fields, "test", typeof(NamespaceOne.SharedName));
            var withoutParent = LinqRuntimeTypeBuilder.Shared.GetDynamicType(fields, "test");

            Assert.NotSame(withParent, withoutParent);
            Assert.Equal(typeof(object), withoutParent.BaseType);
        }
    }
}

namespace NamespaceOne
{
    public class SharedName
    {
        public int Id { get; set; }
    }
}

namespace NamespaceTwo
{
    public class SharedName
    {
        public string? Name { get; set; }
    }
}
