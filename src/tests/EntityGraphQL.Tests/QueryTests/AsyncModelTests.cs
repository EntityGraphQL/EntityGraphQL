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

    /// <summary>user type deliberately in an EntityGraphQL.* namespace - see test below</summary>
    private class SlowDictService
    {
        public System.Threading.Tasks.Task<int> GetValueAsync() => System.Threading.Tasks.Task.FromResult(7);
    }

    [Fact]
    public void AsyncResultWalker_UserTypeInEntityGraphQLNamespace_IsNotRebuilt()
    {
        // the walker rebuilds the engine's own wrapper types and projection types. A USER type that happens
        // to live in an EntityGraphQL.* namespace (like this test assembly) must not be rebuilt into a
        // dynamic type - it is not the engine's
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddScalarType<UserTypeInOurNamespace>("UserBlob", "A scalar-mapped complex value");
        schema.Type<Person>().AddField("userBlob", p => new UserTypeInOurNamespace { Value = 9 }, "A blob");
        schema.Type<Person>().AddField("asyncValue2", "An async value").ResolveAsync<SlowService>((p, srv) => srv.GetValueAsync());

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(new SlowService());
        var sp = serviceCollection.BuildServiceProvider();

        var data = new TestDataContext { People = [new Person { Name = "A" }] };
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { name userBlob asyncValue2 } }" }, data, sp, null);

        Assert.Null(result.Errors);
        dynamic person = ((dynamic)result.Data!["people"]!)[0];
        // still the user's CLR type, not a rebuilt dynamic type
        var blob = Assert.IsType<UserTypeInOurNamespace>((object)person.userBlob);
        Assert.Equal(9, blob.Value);
    }

    [Fact]
    public void AsyncResultWalker_DictionaryStaysADictionary()
    {
        // a dictionary value in a result with async fields must stay a map - previously the collection
        // handling turned it into a List<KeyValuePair<,>> changing the serialized shape
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddScalarType<System.Collections.Generic.Dictionary<string, object>>("JsonMap", "A map");
        schema.Type<Person>().AddField("meta", p => new System.Collections.Generic.Dictionary<string, object> { { "a", 1 }, { "b", "two" } }, "Metadata");
        schema.Type<Person>().AddField("asyncValue3", "An async value").ResolveAsync<SlowDictService>((p, srv) => srv.GetValueAsync());

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(new SlowDictService());
        var sp = serviceCollection.BuildServiceProvider();

        var data = new TestDataContext { People = [new Person { Name = "A" }] };
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ people { name meta asyncValue3 } }" }, data, sp, null);

        Assert.Null(result.Errors);
        dynamic person = ((dynamic)result.Data!["people"]!)[0];
        var meta = Assert.IsAssignableFrom<System.Collections.IDictionary>((object)person.meta);
        Assert.Equal(1, meta["a"]);
        Assert.Equal("two", meta["b"]);
        Assert.Equal(7, person.asyncValue3);
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

/// <summary>
/// Deliberately in the EntityGraphQL.Tests namespace - a user type whose namespace starts with
/// "EntityGraphQL" but is not the engine's assembly
/// </summary>
public class UserTypeInOurNamespace
{
    public int Value { get; set; }
    public System.Threading.Tasks.Task<int>? MaybeTask { get; set; }
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
