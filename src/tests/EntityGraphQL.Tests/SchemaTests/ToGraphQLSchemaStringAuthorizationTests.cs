using System.Linq;
using System.Security.Claims;
using EntityGraphQL.Schema;
using Xunit;
using static EntityGraphQL.Tests.RoleAuthorizationTests;

namespace EntityGraphQL.Tests;

/// <summary>
/// ToGraphQLSchemaString(requestContext) outputs only what that user may access, using the same rules as introspection.
/// </summary>
public class ToGraphQLSchemaStringAuthorizationTests
{
    private static SchemaProvider<RolesDataContext> MakeSchema()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        schema.AddMutationsFrom<RolesMutations>();
        return schema;
    }

    private static QueryRequestContext ContextFor(params string[] roles) => new(null, new ClaimsPrincipal(new ClaimsIdentity(roles.Select(r => new Claim(ClaimTypes.Role, r)), "authed")));

    [Fact]
    public void AnonymousUserSeesNoProtectedTypesOrFields()
    {
        var sdl = MakeSchema().ToGraphQLSchemaString(new QueryRequestContext(null, null));

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
        Assert.Contains("\tname: String!", sdl);
    }

    [Fact]
    public void UserSeesWhatTheirRolesAllow()
    {
        var sdl = MakeSchema().ToGraphQLSchemaString(ContextFor("admin"));

        Assert.Contains("type Project {", sdl);
        Assert.Contains("projects: [Project!]", sdl);
        // admin is not can-type
        Assert.DoesNotContain("\ttype: Int!", sdl);
        Assert.Contains("secret", sdl);
        Assert.DoesNotContain("description", sdl);
    }

    [Fact]
    public void MutationsShowWhenTheUserMayCallThem()
    {
        var sdl = MakeSchema().ToGraphQLSchemaString(ContextFor("can-mutate"));

        Assert.Contains("mutation: Mutation", sdl);
        Assert.Contains("needsAuth", sdl);
    }

    [Fact]
    public void NoContextIsTheFullSchema()
    {
        var schema = MakeSchema();

        Assert.Equal(schema.ToGraphQLSchemaString(), schema.ToGraphQLSchemaString(null));
    }
}
