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

                AddType<Dog>("Dog type").ImplementAllBaseTypes().AddAllFields();
                AddType<Cat>("Cat type").ImplementAllBaseTypes().AddAllFields();
                AddType<PersonType>("Person type").ImplementAllBaseTypes().AddAllFields();

                UpdateQuery(query =>
                {
                    query.AddField(ctx => ctx.Animals, "All animals in the world");
                    query.AddField(ctx => ctx.Dogs, "All dogs in the world");
                    query.AddField(ctx => ctx.People, "All people in the world");
                    query.AddField("animal", new { id = ArgumentHelper.Required<int>() }, (ctx, args) => ctx.Animals.FirstOrDefault(a => a.Id == args.id), "Animal by id");
                    query.AddField("dog", new { id = ArgumentHelper.Required<int>() }, (ctx, args) => ctx.Dogs.FirstOrDefault(a => a.Id == args.id), "Dog by id");
                });

                UpdateType<PersonType>(type =>
                {
                    type.AddField("age", "The name of the person")
                        .ResolveWithService<AgeService>((p, ager) => ager.GetAge(p.Birthday));
                });
            }
        }
    }
}