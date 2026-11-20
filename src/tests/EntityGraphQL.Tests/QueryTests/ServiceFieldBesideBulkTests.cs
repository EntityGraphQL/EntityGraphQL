using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// A root service field that returns an object (OffsetPage) whose items select both a ResolveBulk
/// field and a plain Resolve&lt;TService&gt; scalar. The bulk field forces the two-pass path. The scalar's
/// service parameter must still be injected — it used to fail with "unbound variable: cp".
/// </summary>
public class ServiceFieldBesideBulkTests
{
    public class User
    {
        public string Id { get; set; } = string.Empty;
    }

    public class PageService
    {
        public OffsetPage<User> Page { get; set; } = new(0, 1);

        public System.Threading.Tasks.Task<OffsetPage<User>> GetAsync() => System.Threading.Tasks.Task.FromResult(Page);
    }

    public class Creds
    {
        public int Score { get; set; } = 1;

        public int Current() => Score;
    }

    public class Labels
    {
        public string Label(User user) => user.Id;

        public IDictionary<string, string> LabelsFor(IEnumerable<string> ids) => ids.Distinct().ToDictionary(id => id, id => id);
    }

    [Fact]
    public void ServiceScalarBesideBulk_UnderAsyncServiceRootObject_IsInjected()
    {
        var schema = SchemaBuilder.FromObject<EmptyContext>();
        schema.AddType<User>("User", "A user").AddAllFields();
        schema
            .Type<User>()
            .AddField("label", "Bulk sibling")
            .Resolve<Labels>((user, labels) => labels.Label(user))
            .ResolveBulk<Labels, string, string>(user => user.Id, (ids, labels) => labels.LabelsFor(ids));
        schema.Type<User>().AddField("extra", "Service scalar").Resolve<Creds>((user, cp) => user.Extra(cp.Current()));
        schema.AddType<OffsetPage<User>>("UserPage", "A page").AddAllFields();
        schema.Query().AddField("users", "A page of users").ResolveAsync<PageService>((_, pages) => pages.GetAsync());

        var pages = new PageService
        {
            Page = new OffsetPage<User>(0, 1) { TotalItems = 1, Items = [new User { Id = "abc" }] },
        };
        var services = new ServiceCollection().AddSingleton(pages).AddSingleton(new Creds()).AddSingleton(new Labels()).BuildServiceProvider();

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = "{ users { items { label extra } } }" },
            new EmptyContext(),
            services,
            null,
            new ExecutionOptions { EnableQueryCache = false }
        );

        Assert.Null(result.Errors);
        dynamic users = result.Data!["users"]!;
        dynamic item = ((IEnumerable<dynamic>)users.items).Single();
        Assert.Equal("abc", (string)item.label);
        Assert.Equal(4, (int)item.extra);
    }

    private sealed class EmptyContext;
}

internal static class ServiceFieldBesideBulkExtensions
{
    public static int Extra(this ServiceFieldBesideBulkTests.User user, int current) => user.Id.Length + current;
}
