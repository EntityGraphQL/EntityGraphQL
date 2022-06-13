using System.Linq;
using EntityGraphQL.Schema;

// This is a mock datamodel, what would be your real datamodel and EF context
namespace EntityGraphQL.Tests
{
    /// <summary>
    /// this is a schema that maps back to your current data model, helping you version APIs. You can change your current data model
    /// and keep the API valid by continuing to return the expected objects and fields.
    ///
    /// The key here is that when you change the underlying data model and entities you get a compile error, fixing them to return what is expected
    /// of these classes means you can make non-breaking changes to your exposed API
    /// </summary>
    namespace ApiVersion1
    {
        internal class TestAbstractDataGraphSchema : SchemaProvider<TestAbstractDataContext>
        {
            public TestAbstractDataGraphSchema()
            {
                var animal = AddInterface<Animal>(name: "Animal", description: "An animal");
                animal.AddAllFields();

                AddInheritedType<Dog>(name: "Dog", "", baseType: "Animal");
                AddInheritedType<Cat>(name: "Cat", "", baseType: "Animal");

                UpdateQuery(query =>
                {
                    query.AddField(ctx => ctx.Animals, "All animals in the world");                    
                });
            }
        }
    }
}