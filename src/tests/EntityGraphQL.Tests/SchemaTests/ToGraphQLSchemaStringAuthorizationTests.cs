using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using EntityGraphQL.Authorization;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// ToGraphQLSchemaStringAsync(user) outputs only what that user may access, using the same rules as introspection - and
/// the result must still be valid SDL: hiding something must not leave anything referring to it.
/// </summary>
public class ToGraphQLSchemaStringAuthorizationTests
{
    private static SchemaProvider<RoleAuthorizationTests.RolesDataContext> MakeRolesSchema()
    {
        var schema = SchemaBuilder.FromObject<RoleAuthorizationTests.RolesDataContext>();
        schema.AddMutationsFrom<RoleAuthorizationTests.RolesMutations>();
        return schema;
    }

    private static ClaimsPrincipal UserWith(params string[] roles) => new(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "authed"));

    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    [Fact]
    public async System.Threading.Tasks.Task AnonymousUserSeesNoProtectedTypesOrFields()
    {
        var sdl = await MakeRolesSchema().ToGraphQLSchemaStringAsync(Anonymous);

        Assert.DoesNotContain("type Project", sdl);
        // a field is hidden when its return type is protected, as introspection does
        Assert.DoesNotContain("projects", sdl);
        Assert.DoesNotContain("project:", sdl);
        Assert.DoesNotContain("description", sdl);
        // bare [GraphQLAuthorize] requires an authenticated user
        Assert.DoesNotContain("secret", sdl);
        // every mutation is protected, so there is no mutation root at all
        Assert.DoesNotContain("mutation", sdl);
        Assert.Contains("type Task {", sdl);
        AssertEveryReferencedTypeIsDefined(sdl);
    }

    [Fact]
    public async System.Threading.Tasks.Task UserSeesWhatTheirRolesAllow()
    {
        var sdl = await MakeRolesSchema().ToGraphQLSchemaStringAsync(UserWith("admin"));

        Assert.Contains("type Project {", sdl);
        Assert.Contains("projects: [Project!]", sdl);
        // admin is not can-type
        Assert.DoesNotContain("\ttype: Int!", sdl);
        Assert.Contains("secret", sdl);
        Assert.DoesNotContain("description", sdl);
        AssertEveryReferencedTypeIsDefined(sdl);
    }

    [Fact]
    public async System.Threading.Tasks.Task MutationsShowWhenTheUserMayCallThem()
    {
        var sdl = await MakeRolesSchema().ToGraphQLSchemaStringAsync(UserWith("can-mutate"));

        Assert.Contains("mutation: Mutation", sdl);
        Assert.Contains("needsAuth", sdl);
        AssertEveryReferencedTypeIsDefined(sdl);
    }

    [Fact]
    public void NoContextIsTheFullSchema()
    {
        var schema = MakeRolesSchema();

        Assert.Equal(schema.ToGraphQLSchemaString(), schema.ToGraphQLSchemaString(null));
    }

    /// <summary>The overload on the interface (default implementation) gives the same result</summary>
    [Fact]
    public async System.Threading.Tasks.Task InterfaceOverloadMatches()
    {
        var schema = MakeRolesSchema();
        ISchemaProvider asInterface = schema;

        Assert.Equal(await schema.ToGraphQLSchemaStringAsync(UserWith("admin")), await asInterface.ToGraphQLSchemaStringAsync(UserWith("admin")));
    }

    // --- Things that, hidden, would leave something referring to them ---

    /// <summary>
    /// A: no type-level auth, but every field protected. The type is empty for this user so it is hidden, and so is the
    /// field returning it - otherwise `engine: Engine!` names a type the SDL never defines.
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task TypeEmptiedByFieldAuthIsRemovedWithItsReferences()
    {
        var sdl = await MakeVisibilitySchema().ToGraphQLSchemaStringAsync(Anonymous);

        Assert.DoesNotContain("Engine", sdl);
        Assert.DoesNotContain("engine", sdl);
        Assert.Contains("type Car", sdl);
        AssertEveryReferencedTypeIsDefined(sdl);
    }

    /// <summary>B: a protected interface is dropped from the implements list of the visible type</summary>
    [Fact]
    public async System.Threading.Tasks.Task ProtectedInterfaceIsDroppedFromImplements()
    {
        var sdl = await MakeVisibilitySchema().ToGraphQLSchemaStringAsync(Anonymous);

        Assert.DoesNotContain("INamed", sdl);
        Assert.DoesNotContain("implements", sdl);
        AssertEveryReferencedTypeIsDefined(sdl);
    }

    /// <summary>C: a field is hidden when an argument's type (enum or input) is hidden</summary>
    [Fact]
    public async System.Threading.Tasks.Task FieldWithAHiddenArgumentTypeIsHidden()
    {
        var schema = MakeVisibilitySchema();

        var anonymous = await schema.ToGraphQLSchemaStringAsync(Anonymous);
        Assert.DoesNotContain("carsBy", anonymous);
        Assert.DoesNotContain("Level", anonymous);
        Assert.DoesNotContain("Filter", anonymous);
        AssertEveryReferencedTypeIsDefined(anonymous);

        var admin = await schema.ToGraphQLSchemaStringAsync(UserWith("admin"));
        Assert.Contains("carsBy(", admin);
        AssertEveryReferencedTypeIsDefined(admin);
    }

    /// <summary>D: every query field protected - the query root stays (a schema needs one), as a type with no field block</summary>
    [Fact]
    public async System.Threading.Tasks.Task QueryRootStaysWhenEveryFieldIsHidden()
    {
        var schema = SchemaBuilder.FromObject<AllProtectedContext>();

        var sdl = await schema.ToGraphQLSchemaStringAsync(Anonymous);

        Assert.Contains("query: Query", sdl);
        Assert.Contains("type Query", sdl);
        Assert.DoesNotContain("type Query {", sdl);
        AssertEveryReferencedTypeIsDefined(sdl);
    }

    /// <summary>Introspection hides the same things, so a client building a schema from it gets the same result</summary>
    [Fact]
    public void IntrospectionHidesTheSameTypesAndFields()
    {
        var schema = MakeVisibilitySchema();
        var gql = new QueryRequest
        {
            Query =
                @"{
                    __schema { types { name fields { name } interfaces { name } } }
                    engine: __type(name: ""Engine"") { name }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, new VisibilityContext(), null, Anonymous);

        Assert.Null(result.Errors);
        var types = ((IEnumerable<object>)((dynamic)result.Data!["__schema"]!).types).Cast<dynamic>().ToList();
        var names = types.Select(t => (string)t.name).ToList();
        Assert.DoesNotContain("Engine", names);
        Assert.DoesNotContain("INamed", names);
        Assert.DoesNotContain("Level", names);
        Assert.DoesNotContain("Filter", names);
        var car = types.Single(t => (string)t.name == "Car");
        Assert.DoesNotContain("engine", ((IEnumerable<object>)car.fields).Cast<dynamic>().Select(f => (string)f.name));
        Assert.Empty((IEnumerable<object>)car.interfaces);
        Assert.Null(result.Data["engine"]);
    }

    /// <summary>
    /// Every type the SDL names - field and argument types, implements lists, union members, schema roots - must be
    /// defined in it, or any SDL parser rejects it.
    /// </summary>
    private static void AssertEveryReferencedTypeIsDefined(string sdl)
    {
        var defined = Regex.Matches(sdl, @"^(?:type|input|enum|interface|union|scalar)\s+(\w+)", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToHashSet();

        var referenced = new HashSet<string>();
        // `name: Type`, `arg: [Type!]` - fields, arguments and the schema block roots
        foreach (Match m in Regex.Matches(sdl, @":\s*\[*(\w+)"))
            referenced.Add(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(sdl, @"implements\s+([\w\s&]+?)\s*(?:\{|@|$)", RegexOptions.Multiline))
        {
            foreach (var name in m.Groups[1].Value.Split('&'))
                referenced.Add(name.Trim());
        }
        foreach (Match m in Regex.Matches(sdl, @"^union\s+\w+\s*=\s*(.+)$", RegexOptions.Multiline))
        {
            foreach (var name in m.Groups[1].Value.Split('|'))
                referenced.Add(name.Trim());
        }

        var builtIn = new[] { "String", "Int", "Float", "Boolean", "ID" };
        var undefined = referenced.Where(r => !defined.Contains(r) && !builtIn.Contains(r)).ToList();
        Assert.True(undefined.Count == 0, $"SDL references undefined types: {string.Join(", ", undefined)}\n{sdl}");
    }

    private static SchemaProvider<VisibilityContext> MakeVisibilitySchema()
    {
        var schema = SchemaBuilder.FromObject<VisibilityContext>();

        // A - Engine has no type auth, but its only field is protected
        schema.Type<Engine>().GetField("size", null).RequiresAnyRole("admin");

        // B - a protected interface implemented by a visible type
        var named = schema.AddInterface<INamed>("INamed", "Has a name");
        named.AddAllFields();
        (named.RequiredAuthorization ??= new RequiredAuthorization()).RequiresAnyRole("admin");
        schema.Type<Car>().Implements<INamed>();

        // C - a visible field whose arguments are a protected enum and a protected input type
        schema.AddEnum<Level>("Level", "A level").RequiresAnyRole("admin");
        var filter = schema.AddInputType<Filter>("Filter", "A filter");
        filter.AddAllFields();
        filter.RequiresAnyRole("admin");
        schema.Query().AddField("carsBy", new { level = Level.Low, filter = (Filter?)null }, (ctx, args) => ctx.Cars, "Cars by level");

        return schema;
    }

    public interface INamed
    {
        string Name { get; }
    }

    public class Engine
    {
        public int Size { get; set; }
    }

    public class Car : INamed
    {
        public string Name { get; set; } = string.Empty;
        public Engine Engine { get; set; } = new();
    }

    public enum Level
    {
        Low,
        High,
    }

    public class Filter
    {
        public string? Name { get; set; }
    }

    public class VisibilityContext
    {
        public IEnumerable<Car> Cars { get; set; } = [];
    }

    public class AllProtectedContext
    {
        [GraphQLAuthorize("admin")]
        public IEnumerable<Car> Cars { get; set; } = [];
    }
}
