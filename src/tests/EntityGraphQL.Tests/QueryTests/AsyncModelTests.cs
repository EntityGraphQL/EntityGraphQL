using System.Linq;
using System.Threading;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using BlobWithLazyProperty = UserApp.Entities.BlobWithLazyProperty;

namespace EntityGraphQL.Tests
{

/// <summary>
/// Tests for the 6.0 async model - CancellationToken plumbing and the async result walker
/// </summary>
public class AsyncModelTests
{
    private class TokenMutations
    {
        public static CancellationToken? SeenToken;

        [GraphQLMutation]
        public static async System.Threading.Tasks.Task<int> DoWork(TestDataContext db, int value, CancellationToken cancellationToken)
        {
            SeenToken = cancellationToken;
            await System.Threading.Tasks.Task.Delay(1, cancellationToken);
            return value * 2;
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task Mutation_WithCancellationTokenParameter_ReceivesRequestToken()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddMutationsFrom<TokenMutations>();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(new TestDataContext());
        var sp = serviceCollection.BuildServiceProvider();

        using var cts = new CancellationTokenSource();
        TokenMutations.SeenToken = null;
        var result = await schema.ExecuteRequestAsync(new QueryRequest { Query = "mutation { doWork(value: 21) }" }, sp, null, null, cts.Token);

        Assert.Null(result.Errors);
        Assert.Equal(42, result.Data!["doWork"]);
        // the token passed into the mutation method is the request's token, not default
        Assert.Equal(cts.Token, TokenMutations.SeenToken);
    }

    [Fact]
    public void Mutation_WithCancellationTokenParameter_TokenIsNotASchemaArgument()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddMutationsFrom<TokenMutations>();

        var sdl = schema.ToGraphQLSchemaString();
        Assert.Contains("doWork(value: Int!)", sdl);
        Assert.DoesNotContain("cancellationToken", sdl);
    }

    private class SlowService
    {
        public System.Threading.Tasks.Task<int> GetValueAsync() => System.Threading.Tasks.Task.FromResult(5);
    }

    [Fact]
    public void AsyncResultWalker_DoesNotReadPropertiesOfNonAsyncTypes()
    {
        // when a query has an async field the whole result is walked to await pending tasks. Types whose
        // shape can not contain an async value must not be reflected over - reading every property of an
        // entity can trigger EF lazy-loading of navigations the query never selected
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddScalarType<BlobWithLazyProperty>("Blob", "A scalar-mapped complex value");
        // blob and the async field are part of the same object so the async walker visits the blob value
        schema.Type<Person>().AddField("blob", p => new BlobWithLazyProperty(), "A blob");
        schema.Type<Person>().AddField("asyncValue", "An async value").ResolveAsync<SlowService>((p, srv) => srv.GetValueAsync());

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(new SlowService());
        var sp = serviceCollection.BuildServiceProvider();

        var data = new TestDataContext { People = [new Person { Name = "A" }] };
        BlobWithLazyProperty.LazyReads = 0;
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { name blob asyncValue } }" }, data, sp, null);

        Assert.Null(result.Errors);
        dynamic person = ((dynamic)result.Data!["people"]!)[0];
        Assert.Equal(5, person.asyncValue);
        Assert.Equal("blob", ((BlobWithLazyProperty)person.blob).Name);
        Assert.Equal(0, BlobWithLazyProperty.LazyReads);
    }
}
}

namespace UserApp.Entities
{
    /// <summary>
    /// Simulates an EF entity with a lazy-loaded navigation - reading the property has a side effect.
    /// Lives outside the EntityGraphQL.* namespaces like real user entities do.
    /// </summary>
    public class BlobWithLazyProperty
    {
        public static int LazyReads;
        public string Name { get; set; } = "blob";

        private BlobWithLazyProperty? related;
        public BlobWithLazyProperty? Related
        {
            get
            {
                LazyReads++;
                return related;
            }
            set => related = value;
        }
    }
}
