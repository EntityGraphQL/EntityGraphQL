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
        public List<string> Tags { get; set; } = [];

        public int Instance(int current) => Id.Length + current;
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

        public Box Box(IEnumerable<string> tags) => new() { Items = tags.Select(t => new Wrapped { V = t }).ToList() };
    }

    public class Wrapped
    {
        public string V { get; set; } = string.Empty;
    }

    public class Box
    {
        public List<Wrapped> Items { get; set; } = [];
    }

    public class Labels
    {
        public string Label(User user) => user.Id;

        public IDictionary<string, string> LabelsFor(IEnumerable<string> ids) => ids.Distinct().ToDictionary(id => id, id => id);
    }

    [Fact]
    public void ExtensionMethodTakingService_IsInjected()
    {
        // "abc".Length + Creds.Current()
        Assert.Equal(4, (int)Run(schema => schema.Type<User>().AddField("extra", "Service scalar").Resolve<Creds>((user, cp) => user.Extra(cp.Current()))).extra);
    }

    [Fact]
    public void InstanceMethodTakingService_IsInjected()
    {
        Assert.Equal(4, (int)Run(schema => schema.Type<User>().AddField("extra", "Service scalar").Resolve<Creds>((user, cp) => user.Instance(cp.Current()))).extra);
    }

    [Fact]
    public void ServiceInsideLambda_IsInjected()
    {
        // tags with the length Creds.Current() returns
        Assert.Equal(1, (int)Run(schema => schema.Type<User>().AddField("extra", "Service scalar").Resolve<Creds>((user, cp) => user.Tags.Count(t => t.Length == cp.Current()))).extra);
    }

    [Fact]
    public void ContextReadInsideServicePredicate_KeepsLambdaParameterInScope()
    {
        var item = Run(schema => schema.Type<User>().AddField("extra", "Matching tags").Resolve<Creds>((user, cp) => user.Tags.Count(t => user.Id.StartsWith(t) && cp.Current() > 0)));
        Assert.Equal(1, (int)item.extra);
    }

    private static IEnumerable<Wrapped> Catalog() => [new() { V = "abc" }, new() { V = "other" }];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PagedStaticMethodSource_WithServicePredicate(bool connectionPaging)
    {
        var item = Run(
            schema =>
            {
                schema.AddType<Wrapped>("Wrapped", "Catalog entry").AddAllFields();
                var field = schema.Type<User>().AddField("extra", "Matching entries").Resolve<Creds>((user, cp) => Catalog().Where(entry => entry.V == user.Id && cp.Current() > 0));
                if (connectionPaging)
                    field.UseConnectionPaging();
                else
                    field.UseOffsetPaging();
            },
            connectionPaging ? "extra(first: 2) { edges { node { v } } totalCount }" : "extra(take: 2) { items { v } totalItems }"
        );
        Assert.Equal(1, connectionPaging ? (int)item.extra.totalCount : (int)item.extra.totalItems);
    }

    [Fact]
    public void ArgumentsStayInTheFirstPass_AndAreBoundPerAlias()
    {
        Field? field = null;
        var item = Run(
            schema => field = schema.Type<User>().AddField("extra", new { n = 0 }, "Service scalar").Resolve<Creds>((user, args, cp) => cp.Current() + user.Tags.Take(args.n).Count()),
            "a: extra(n: 1) b: extra(n: 2)"
        );

        // the whole context read is extracted, not all of user.Tags
        Assert.Equal("user.Tags.Take(args.n).Count()", Assert.Single(field!.ExtractedFieldsFromServices!).FieldExpressions.First().ToString());
        Assert.Equal(2, (int)item.a);
        Assert.Equal(3, (int)item.b);
    }

    [Fact]
    public void ArgumentValidators_RunOncePerSelectionInTheFirstPass()
    {
        var count = 0;
        var item = Run(
            schema =>
                schema
                    .Type<User>()
                    .AddField("extra", new { n = 0 }, "Service scalar")
                    // two context reads use the arguments, so two expressions are extracted
                    .Resolve<Creds>((user, args, cp) => cp.Current() + user.Tags.Take(args.n).Count() + user.Tags.Skip(args.n).Count())
                    .AddValidator(_ => count++),
            "a: extra(n: 1)"
        );

        Assert.Equal(4, (int)item.a);
        // once where the extracted expressions are bound and once where the field itself is built
        Assert.Equal(2, count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PagedCollectionFromAService_IsNotInterleaved(bool connectionPaging)
    {
        var item = Run(
            schema =>
            {
                schema.AddType<Wrapped>("Wrapped", "A wrapped tag").AddAllFields();
                var field = schema.Type<User>().AddField("extra", "Paged service list").Resolve<Creds>((user, cp) => cp.Box(user.Tags).Items);
                if (connectionPaging)
                    field.UseConnectionPaging();
                else
                    field.UseOffsetPaging();
            },
            connectionPaging ? "extra(first: 2) { edges { node { v } } totalCount }" : "extra(take: 2) { items { v } totalItems }"
        );

        Assert.Equal(3, connectionPaging ? (int)item.extra.totalCount : (int)item.extra.totalItems);
    }

    /// <summary>
    /// Selects the field added to User beside a ResolveBulk field, on the items of a page from an async service.
    /// </summary>
    private static dynamic Run(System.Action<SchemaProvider<EmptyContext>> addExtraField, string selection = "extra")
    {
        var schema = SchemaBuilder.FromObject<EmptyContext>();
        schema.AddType<User>("User", "A user").AddAllFields();
        schema
            .Type<User>()
            .AddField("label", "Bulk sibling")
            .Resolve<Labels>((user, labels) => labels.Label(user))
            .ResolveBulk<Labels, string, string>(user => user.Id, (ids, labels) => labels.LabelsFor(ids));
        addExtraField(schema);
        schema.AddType<OffsetPage<User>>("UserPage", "A page").AddAllFields();
        schema.Query().AddField("users", "A page of users").ResolveAsync<PageService>((_, pages) => pages.GetAsync());

        var pages = new PageService
        {
            Page = new OffsetPage<User>(0, 1) { TotalItems = 1, Items = [new User { Id = "abc", Tags = ["a", "bb", "ccc"] }] },
        };
        var services = new ServiceCollection().AddSingleton(pages).AddSingleton(new Creds()).AddSingleton(new Labels()).BuildServiceProvider();

        var result = schema.ExecuteRequestWithContext(
            new QueryRequest { Query = $"{{ users {{ items {{ label {selection} }} }} }}" },
            new EmptyContext(),
            services,
            null,
            new ExecutionOptions { EnableQueryCache = false }
        );

        Assert.Null(result.Errors);
        dynamic users = result.Data!["users"]!;
        dynamic item = ((IEnumerable<dynamic>)users.items).Single();
        Assert.Equal("abc", (string)item.label);
        return item;
    }

    private sealed class EmptyContext;
}

internal static class ServiceFieldBesideBulkExtensions
{
    public static int Extra(this ServiceFieldBesideBulkTests.User user, int current) => user.Id.Length + current;
}
