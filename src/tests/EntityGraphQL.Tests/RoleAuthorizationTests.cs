using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using EntityGraphQL.Authorization;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class RoleAuthorizationTests
{
    [Fact]
    public void TestAttributeOnTypeFromObject()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();

        Assert.Single(schema.Type<Project>().RequiredAuthorization!.GetRoles()!);
        Assert.Equal("admin", schema.Type<Project>().RequiredAuthorization!.GetRoles()!.ElementAt(0).ElementAt(0));
    }

    [Fact]
    public void TestAttributeOnTypeAddType()
    {
        var schema = new SchemaProvider<object>();
        schema.AddType<Project>("Project", "All about the project");

        Assert.Single(schema.Type<Project>().RequiredAuthorization!.GetRoles()!);
        Assert.Equal("admin", schema.Type<Project>().RequiredAuthorization!.GetRoles()!.ElementAt(0).ElementAt(0));
    }

    [Fact]
    public void TestMethodOnType()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();

        Assert.Null(schema.Type<Task>().RequiredAuthorization);

        schema.Type<Task>().RequiresAnyRole("admin");

        Assert.Single(schema.Type<Task>().RequiredAuthorization!.GetRoles()!);
        Assert.Equal("admin", schema.Type<Task>().RequiredAuthorization!.GetRoles()!.ElementAt(0).ElementAt(0));
    }

    [Fact]
    public void TestAttributeOnField()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();

        Assert.Single(schema.Type<Project>().GetField("type", null).RequiredAuthorization!.GetRoles()!);
        Assert.Equal("can-type", schema.Type<Project>().GetField("type", null).RequiredAuthorization!.GetRoles()!.ElementAt(0).ElementAt(0));
    }

    [Fact]
    public void TestAttributeOnFieldAddField()
    {
        var schema = new SchemaProvider<object>();
        schema.AddType<Project>("Project", "All about the project").AddField(p => p.Type, "The type info");

        Assert.Single(schema.Type<Project>().GetField("type", null).RequiredAuthorization!.GetRoles()!);
        Assert.Equal("can-type", schema.Type<Project>().GetField("type", null).RequiredAuthorization!.GetRoles()!.ElementAt(0).ElementAt(0));
    }

    [Fact]
    public void TestMethodOnField()
    {
        var schema = new SchemaProvider<object>();

        schema.AddType<Task>("Task", "All about tasks").AddField(p => p.IsActive, "Is it active").RequiresAnyRole("admin");

        Assert.Single(schema.Type<Task>().GetField("isActive", null).RequiredAuthorization!.GetRoles()!);
        Assert.Equal("admin", schema.Type<Task>().GetField("isActive", null).RequiredAuthorization!.GetRoles()!.ElementAt(0).ElementAt(0));
    }

    [Fact]
    public void TestRequiresAnyRoleMany()
    {
        var schema = new SchemaProvider<object>();

        schema.AddType<Task>("Task", "All about tasks").AddField(p => p.IsActive, "Is it active").RequiresAnyRole("admin", "something-else");

        Assert.Single(schema.Type<Task>().GetField("isActive", null).RequiredAuthorization!.GetRoles()!);
        Assert.Equal("admin", schema.Type<Task>().GetField("isActive", null).RequiredAuthorization!.GetRoles()!.ElementAt(0).ElementAt(0));
        Assert.Equal("something-else", schema.Type<Task>().GetField("isActive", null).RequiredAuthorization!.GetRoles()!.ElementAt(0).ElementAt(1));
    }

    [Fact]
    public void TestFieldIsSecured()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();

        var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin") }, "authed");
        var gql = new QueryRequest
        {
            Query =
                @"{
                    projects { type }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

        Assert.Equal("Field 'projects' - You are not authorized to access the 'type' field on type 'Project'.", result.Errors!.First().Message);

        claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin"), new Claim(ClaimTypes.Role, "can-type") }, "authed");
        result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

        Assert.Null(result.Errors);
    }

    [Fact]
    public void TestTypeIsSecured()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();

        var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "not-admin") }, "authed");
        var gql = new QueryRequest
        {
            Query =
                @"{
                    projects { id }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

        Assert.Equal("Field 'projects' - You are not authorized to access the 'Project' type returned by field 'projects'.", result.Errors!.First().Message);

        claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin") }, "authed");
        result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

        Assert.Null(result.Errors);
    }

    [Fact]
    public void TestTypeIsSecuredWithNullUser()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();

        var gql = new QueryRequest
        {
            Query =
                @"{
                    projects { id }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, null);

        Assert.NotNull(result.Errors);
        Assert.Equal("Field 'projects' - You are not authorized to access the 'Project' type returned by field 'projects'.", result.Errors.First().Message);
    }

    [Fact]
    public void TestNonTopLevelTypeIsSecured()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();

        var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "not-admin") }, "authed");
        var gql = new QueryRequest
        {
            Query =
                @"{
                    tasks {
                        project { id }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

        Assert.Equal("Field 'tasks' - You are not authorized to access the 'Project' type returned by field 'project'.", result.Errors!.First().Message);

        claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin") }, "authed");
        result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

        Assert.Null(result.Errors);
    }

    [Fact]
    public void TestQueryType()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        schema.Query().RequiresAnyRole("admin", "half-admin");

        var claims = new ClaimsIdentity([new Claim(ClaimTypes.Role, "not-admin")], "authed");
        var gql = new QueryRequest
        {
            Query =
                @"{
                    tasks {
                        id
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

        Assert.Equal("You are not authorized to access the 'Query' type.", result.Errors!.First().Message);

        claims = new ClaimsIdentity([new Claim(ClaimTypes.Role, "admin")], "authed");
        result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));
        Assert.Null(result.Errors);

        claims = new ClaimsIdentity([new Claim(ClaimTypes.Role, "half-admin")], "authed");
        result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));
        Assert.Null(result.Errors);
    }

    [Fact]
    public void TestFieldIsSecuredWithAnyRole()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        schema.Type<Task>().ReplaceField("name", t => t.Name, "Task name").RequiresAnyRole("admin", "half-admin");

        var claims = new ClaimsIdentity([new Claim(ClaimTypes.Role, "not-admin")], "authed");
        var gql = new QueryRequest
        {
            Query =
                @"{
                    tasks {
                        id
                        name
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

        Assert.Equal("Field 'tasks' - You are not authorized to access the 'name' field on type 'Task'.", result.Errors!.First().Message);

        claims = new ClaimsIdentity([new Claim(ClaimTypes.Role, "admin")], "authed");
        result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));
        Assert.Null(result.Errors);

        claims = new ClaimsIdentity([new Claim(ClaimTypes.Role, "half-admin")], "authed");
        result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));
        Assert.Null(result.Errors);
    }

    [Fact]
    public void TestGraphQLFieldAttributeSecure()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();

        var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "not-admin") }, "authed");
        var gql = new QueryRequest
        {
            Query =
                @"{
                    tasks {
                        id description
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

        Assert.Equal("Field 'tasks' - You are not authorized to access the 'description' field on type 'Task'.", result.Errors!.First().Message);

        claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "can-description") }, "authed");
        result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

        Assert.Null(result.Errors);
    }

    [Fact]
    public void TestMutationAuth()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        schema.AddMutationsFrom<RolesMutations>();

        var claims = new ClaimsIdentity([new Claim(ClaimTypes.Role, "not-admin")], "authed");
        var gql = new QueryRequest { Query = @"mutation T { needsAuth }" };

        var result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

        Assert.NotNull(result.Errors);
        Assert.Equal("You are not authorized to access the 'needsAuth' field on type 'Mutation'.", result.Errors.First().Message);
        Assert.Equal(["needsAuth"], result.Errors.First().Path);

        claims = new ClaimsIdentity([new Claim(ClaimTypes.Role, "can-mutate")], "authed");
        result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

        Assert.Null(result.Errors);
    }

    // ── §2.4 auth on fragments / aliases ──────────────────────────────────────

    [Fact]
    public void Auth_FragmentSpread_ProtectedField_IsBlocked()
    {
        // A protected field accessed through a named fragment spread must still require auth.
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        var gql = new QueryRequest
        {
            Query =
                @"
                query { tasks { ...TaskDetails } }
                fragment TaskDetails on Task { id description }",
        };

        var noClaim = new ClaimsIdentity([new Claim(ClaimTypes.Role, "other")], "authed");
        var fail = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(noClaim));
        Assert.NotNull(fail.Errors);
        Assert.Contains(fail.Errors!, e => e.Message.Contains("description"));

        var withClaim = new ClaimsIdentity([new Claim(ClaimTypes.Role, "can-description")], "authed");
        var pass = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(withClaim));
        Assert.Null(pass.Errors);
    }

    [Fact]
    public void Auth_FragmentSpread_ProtectedType_IsBlocked()
    {
        // A fragment spread that expands into a protected type (Project requires "admin") must block access.
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        var gql = new QueryRequest
        {
            Query =
                @"
                query { tasks { project { ...ProjectDetails } } }
                fragment ProjectDetails on Project { id }",
        };

        var noClaim = new ClaimsIdentity([new Claim(ClaimTypes.Role, "other")], "authed");
        var fail = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(noClaim));
        Assert.NotNull(fail.Errors);
        Assert.Contains(fail.Errors!, e => e.Message.Contains("Project"));

        var withClaim = new ClaimsIdentity([new Claim(ClaimTypes.Role, "admin")], "authed");
        var pass = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(withClaim));
        Assert.Null(pass.Errors);
    }

    [Fact]
    public void Auth_InlineFragment_ProtectedField_IsBlocked()
    {
        // A protected field accessed through an inline fragment must still require auth.
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        var gql = new QueryRequest { Query = @"{ tasks { ... on Task { id description } } }" };

        var noClaim = new ClaimsIdentity([new Claim(ClaimTypes.Role, "other")], "authed");
        var fail = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(noClaim));
        Assert.NotNull(fail.Errors);
        Assert.Contains(fail.Errors!, e => e.Message.Contains("description"));

        var withClaim = new ClaimsIdentity([new Claim(ClaimTypes.Role, "can-description")], "authed");
        var pass = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(withClaim));
        Assert.Null(pass.Errors);
    }

    [Fact]
    public void Auth_InlineFragment_ProtectedType_IsBlocked()
    {
        // An inline fragment on a type-guarded type (Project requires "admin") must block access.
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        var gql = new QueryRequest { Query = @"{ tasks { project { ... on Project { id } } } }" };

        var noClaim = new ClaimsIdentity([new Claim(ClaimTypes.Role, "other")], "authed");
        var fail = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(noClaim));
        Assert.NotNull(fail.Errors);
        Assert.Contains(fail.Errors!, e => e.Message.Contains("Project"));

        var withClaim = new ClaimsIdentity([new Claim(ClaimTypes.Role, "admin")], "authed");
        var pass = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(withClaim));
        Assert.Null(pass.Errors);
    }

    [Fact]
    public void Auth_Alias_ProtectedField_IsBlocked()
    {
        // Aliasing a protected field must not bypass its authorization requirement.
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        var gql = new QueryRequest { Query = @"{ tasks { aliasedDesc: description } }" };

        var noClaim = new ClaimsIdentity([new Claim(ClaimTypes.Role, "other")], "authed");
        var fail = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(noClaim));
        Assert.NotNull(fail.Errors);
        Assert.Contains(fail.Errors!, e => e.Message.Contains("description"));

        var withClaim = new ClaimsIdentity([new Claim(ClaimTypes.Role, "can-description")], "authed");
        var pass = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(withClaim));
        Assert.Null(pass.Errors);
    }

    // ── fail-closed: a bare [GraphQLAuthorize] means "any authenticated user" ──────────────

    [Fact]
    public void BareAuthorize_ProducesPresentRequiredAuthorization()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        // a bare [GraphQLAuthorize] (no roles) must still produce a RequiredAuthorization so it is enforced
        var auth = schema.Type<Task>().GetField("secret", null).RequiredAuthorization;
        Assert.NotNull(auth);
        // no roles required - just authentication
        Assert.True(auth!.GetRoles() == null || !auth.GetRoles()!.Any());
    }

    [Fact]
    public void BareAuthorize_BlocksAnonymous_AllowsAuthenticated()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        var gql = new QueryRequest { Query = @"{ tasks { id secret } }" };

        // anonymous (no user) is denied
        var anon = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, null);
        Assert.NotNull(anon.Errors);
        Assert.Contains(anon.Errors!, e => e.Message.Contains("secret"));

        // an unauthenticated identity is denied (IsAuthenticated == false)
        var notAuthed = new ClaimsPrincipal(new ClaimsIdentity());
        var notAuthedResult = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, notAuthed);
        Assert.NotNull(notAuthedResult.Errors);
        Assert.Contains(notAuthedResult.Errors!, e => e.Message.Contains("secret"));

        // any authenticated user (no particular role) is allowed
        var authed = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "someone")], "authed"));
        var pass = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, authed);
        Assert.Null(pass.Errors);
    }

    [Fact]
    public void IsAuthorized_NullRequiredAuth_IsOpen()
    {
        var auth = new RoleBasedAuthorization();
        Assert.True(auth.IsAuthorized(null, null));
        Assert.True(auth.IsAuthorized(new ClaimsPrincipal(new ClaimsIdentity([], "authed")), null));
    }

    [Fact]
    public void IsAuthorized_PresentEmptyAuth_RequiresAuthentication()
    {
        var auth = new RoleBasedAuthorization();
        var required = new RequiredAuthorization(); // present but empty

        Assert.False(auth.IsAuthorized(null, required));
        Assert.False(auth.IsAuthorized(new ClaimsPrincipal(new ClaimsIdentity()), required)); // not authenticated
        Assert.True(auth.IsAuthorized(new ClaimsPrincipal(new ClaimsIdentity([], "authed")), required));
    }

    [Fact]
    public void IsAuthorized_RoleAuth_RequiresRoleAndAuthentication()
    {
        var auth = new RoleBasedAuthorization();
        var required = new RequiredAuthorization();
        required.RequiresAnyRole("admin");

        Assert.False(auth.IsAuthorized(null, required));
        Assert.False(auth.IsAuthorized(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "user")], "authed")), required));
        Assert.True(auth.IsAuthorized(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "admin")], "authed")), required));
    }

    internal class RolesDataContext
    {
        public IEnumerable<Project> Projects { get; set; } = new List<Project>();
        public IEnumerable<Task> Tasks { get; set; } = new List<Task>();
    }

    [GraphQLAuthorize("admin")]
    internal class Project
    {
        public int Id { get; set; }

        [GraphQLAuthorize("can-type")]
        public int Type { get; set; }
        public IEnumerable<Task> Tasks { get; set; } = [];
    }

    internal class Task
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public Project? Project { get; set; }

        [GraphQLAuthorize("can-description")]
        [GraphQLField("description")]
        public string GetDescription()
        {
            return "This is a description";
        }

        // bare [GraphQLAuthorize] with no roles - requires an authenticated user only
        [GraphQLAuthorize]
        public string Secret { get; set; } = "shh";
    }

    internal class RolesMutations
    {
        [GraphQLAuthorize("can-mutate")]
        [GraphQLMutation]
        public static bool NeedsAuth()
        {
            return true;
        }
    }
}
