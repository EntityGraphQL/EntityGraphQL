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

    // ── RequiresAllRoles (AND) ────────────────────────────────────────────────
    // These guard the shape difference against RequiresAnyRole: AddAnyRole puts every role in ONE group
    // (OR), AddAllRoles puts each role in its OWN group (AND). RoleBasedAuthorization ANDs the groups and
    // ORs within them, so swapping the two implementations turns "all of" into "any of" - i.e. it grants
    // access to users holding a single role.

    [Fact]
    public void RequiresAllRoles_OnField_AddsOneGroupPerRole()
    {
        var schema = new SchemaProvider<object>();
        schema.AddType<Task>("Task", "All about tasks").AddField(p => p.IsActive, "Is it active").RequiresAllRoles("admin", "something-else");

        var roles = schema.Type<Task>().GetField("isActive", null).RequiredAuthorization!.GetRoles()!;
        // AND = one group per role, each with a single role in it
        Assert.Equal(2, roles.Count());
        Assert.Equal(["admin"], roles.ElementAt(0));
        Assert.Equal(["something-else"], roles.ElementAt(1));
    }

    [Fact]
    public void RequiresAnyRole_AddsSingleGroup_UnlikeRequiresAllRoles()
    {
        var any = new RequiredAuthorization();
        any.RequiresAnyRole("admin", "something-else");
        var all = new RequiredAuthorization();
        all.RequiresAllRoles("admin", "something-else");

        // OR: one group holding both roles
        Assert.Single(any.GetRoles()!);
        Assert.Equal(["admin", "something-else"], any.GetRoles()!.ElementAt(0));
        // AND: two groups, one role each
        Assert.Equal(2, all.GetRoles()!.Count());
    }

    [Fact]
    public void RequiresAllRoles_OnField_UserMustHoldEveryRole()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        schema.Type<Task>().ReplaceField("name", t => t.Name, "Task name").RequiresAllRoles("admin", "half-admin");

        var gql = new QueryRequest { Query = @"{ tasks { id name } }" };

        // holding only one of the two required roles is not enough
        var onlyAdmin = new ClaimsIdentity([new Claim(ClaimTypes.Role, "admin")], "authed");
        var fail = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(onlyAdmin));
        Assert.NotNull(fail.Errors);
        Assert.Equal("Field 'tasks' - You are not authorized to access the 'name' field on type 'Task'.", fail.Errors!.First().Message);

        var onlyHalf = new ClaimsIdentity([new Claim(ClaimTypes.Role, "half-admin")], "authed");
        var fail2 = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(onlyHalf));
        Assert.NotNull(fail2.Errors);

        // both roles - allowed
        var both = new ClaimsIdentity([new Claim(ClaimTypes.Role, "admin"), new Claim(ClaimTypes.Role, "half-admin")], "authed");
        var pass = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(both));
        Assert.Null(pass.Errors);
    }

    [Fact]
    public void RequiresAllRoles_OnType_UserMustHoldEveryRole()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        schema.Type<Task>().RequiresAllRoles("admin", "half-admin");

        var gql = new QueryRequest { Query = @"{ tasks { id } }" };

        var onlyAdmin = new ClaimsIdentity([new Claim(ClaimTypes.Role, "admin")], "authed");
        var fail = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(onlyAdmin));
        Assert.NotNull(fail.Errors);
        Assert.Contains(fail.Errors!, e => e.Message.Contains("'Task' type"));

        var both = new ClaimsIdentity([new Claim(ClaimTypes.Role, "admin"), new Claim(ClaimTypes.Role, "half-admin")], "authed");
        var pass = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(both));
        Assert.Null(pass.Errors);
    }

    [Fact]
    public void RequiresAllRoles_OnRequiredAuthorization_IsEnforced()
    {
        var auth = new RoleBasedAuthorization();
        var required = new RequiredAuthorization();
        required.RequiresAllRoles("admin", "half-admin");

        Assert.False(auth.IsAuthorized(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "admin")], "authed")), required));
        Assert.True(
            auth.IsAuthorized(
                new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "admin"), new Claim(ClaimTypes.Role, "half-admin")], "authed")),
                required
            )
        );
    }

    [Fact]
    public void ClearRoles_RemovesRoleRequirement()
    {
        var required = new RequiredAuthorization();
        required.RequiresAnyRole("admin");
        Assert.True(required.Any());

        required.ClearRoles();

        Assert.Null(required.GetRoles());
        Assert.False(required.Any());
        // an empty-but-present RequiredAuthorization still requires authentication, but no longer a role
        var auth = new RoleBasedAuthorization();
        Assert.True(auth.IsAuthorized(new ClaimsPrincipal(new ClaimsIdentity([], "authed")), required));
        Assert.False(auth.IsAuthorized(new ClaimsPrincipal(new ClaimsIdentity()), required));
    }

    [Fact]
    public void Clear_RemovesAllAuthorizationData()
    {
        var required = new RequiredAuthorization();
        required.RequiresAnyRole("admin");
        required.SetData("other-impl", [["x"]]);
        Assert.Equal(2, required.AuthData.Count);

        required.Clear();

        Assert.False(required.Any());
        Assert.Empty(required.AuthData);
        Assert.False(required.TryGetData("other-impl", out _));
    }

    // ── Concat: the only place auth requirements merge ────────────────────────

    [Fact]
    public void Concat_SameKey_KeepsBothRequirements()
    {
        var left = new RequiredAuthorization();
        left.RequiresAnyRole("admin");
        var right = new RequiredAuthorization();
        right.RequiresAnyRole("can-type");

        var merged = left.Concat(right);

        // both groups survive - the user must satisfy each (AND)
        var roles = merged.GetRoles()!;
        Assert.Equal(2, roles.Count());
        Assert.Equal(["admin"], roles.ElementAt(0));
        Assert.Equal(["can-type"], roles.ElementAt(1));

        var auth = new RoleBasedAuthorization();
        Assert.False(auth.IsAuthorized(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "admin")], "authed")), merged));
        Assert.True(
            auth.IsAuthorized(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "admin"), new Claim(ClaimTypes.Role, "can-type")], "authed")), merged)
        );
    }

    [Fact]
    public void Concat_DifferentKeys_KeepsBoth()
    {
        var left = new RequiredAuthorization();
        left.RequiresAnyRole("admin");
        var right = new RequiredAuthorization();
        right.SetData("other-impl", [["policy-a"]]);

        var merged = left.Concat(right);

        Assert.Equal(2, merged.AuthData.Count);
        Assert.Single(merged.GetRoles()!);
        Assert.True(merged.TryGetData("other-impl", out var other));
        Assert.Equal(["policy-a"], other!.ElementAt(0));
    }

    [Fact]
    public void Concat_DoesNotMutateEitherOperand()
    {
        // Concat is called while building the schema (class-level + method-level auth, [Authorize] +
        // [GraphQLAuthorize]) and the operands are shared - a class-level RequiredAuthorization is passed to
        // every method on that class. Mutating an operand would leak one field's requirements onto another.
        var classLevel = new RequiredAuthorization();
        classLevel.RequiresAnyRole("class-role");
        var methodA = new RequiredAuthorization();
        methodA.RequiresAnyRole("method-a");
        var methodB = new RequiredAuthorization();
        methodB.RequiresAnyRole("method-b");

        var mergedA = methodA.Concat(classLevel);
        var mergedB = methodB.Concat(classLevel);

        Assert.Single(classLevel.GetRoles()!);
        Assert.Single(methodA.GetRoles()!);
        Assert.Single(methodB.GetRoles()!);
        Assert.Equal(2, mergedA.GetRoles()!.Count());
        Assert.Equal(2, mergedB.GetRoles()!.Count());
        Assert.DoesNotContain(mergedA.GetRoles()!, g => g.Contains("method-b"));
    }

    [Fact]
    public void Concat_EmptyOperands_StillRequiresAuthentication()
    {
        // a bare [GraphQLAuthorize] on the class and on the method - present but empty, must stay present
        var merged = new RequiredAuthorization().Concat(new RequiredAuthorization());
        var auth = new RoleBasedAuthorization();
        Assert.False(auth.IsAuthorized(new ClaimsPrincipal(new ClaimsIdentity()), merged));
        Assert.True(auth.IsAuthorized(new ClaimsPrincipal(new ClaimsIdentity([], "authed")), merged));
    }

    // ── class-level auth on a mutation/subscription controller ────────────────

    [Fact]
    public void MutationAuth_ClassLevelRole_AppliesToMethodWithNoOwnAuth()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        schema.AddMutationsFrom<ClassLevelRolesMutations>();

        var gql = new QueryRequest { Query = @"mutation T { classOnly }" };

        var wrongRole = new ClaimsIdentity([new Claim(ClaimTypes.Role, "not-it")], "authed");
        var fail = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(wrongRole));
        Assert.NotNull(fail.Errors);
        Assert.Equal("You are not authorized to access the 'classOnly' field on type 'Mutation'.", fail.Errors!.First().Message);

        var classRole = new ClaimsIdentity([new Claim(ClaimTypes.Role, "class-role")], "authed");
        var pass = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(classRole));
        Assert.Null(pass.Errors);
    }

    [Fact]
    public void MutationAuth_ClassLevelAndMethodLevelRoles_BothRequired()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        schema.AddMutationsFrom<ClassLevelRolesMutations>();

        var gql = new QueryRequest { Query = @"mutation T { classAndMethod }" };

        // the class-level role alone is not enough
        var classOnly = new ClaimsIdentity([new Claim(ClaimTypes.Role, "class-role")], "authed");
        var fail = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(classOnly));
        Assert.NotNull(fail.Errors);

        // nor is the method-level role alone
        var methodOnly = new ClaimsIdentity([new Claim(ClaimTypes.Role, "method-role")], "authed");
        var fail2 = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(methodOnly));
        Assert.NotNull(fail2.Errors);

        var both = new ClaimsIdentity([new Claim(ClaimTypes.Role, "class-role"), new Claim(ClaimTypes.Role, "method-role")], "authed");
        var pass = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(both));
        Assert.Null(pass.Errors);
    }

    [Fact]
    public void MutationAuth_ClassLevelRole_DoesNotLeakBetweenMethods()
    {
        // classAndMethod requires "method-role" on top of the class role - classOnly must not pick that up
        var schema = SchemaBuilder.FromObject<RolesDataContext>();
        schema.AddMutationsFrom<ClassLevelRolesMutations>();

        var classOnlyRoles = schema.Mutation().SchemaType.GetField("classOnly", null).RequiredAuthorization!.GetRoles()!;
        Assert.Single(classOnlyRoles);
        Assert.Equal(["class-role"], classOnlyRoles.ElementAt(0));

        var bothRoles = schema.Mutation().SchemaType.GetField("classAndMethod", null).RequiredAuthorization!.GetRoles()!;
        Assert.Equal(2, bothRoles.Count());
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

    // class-level auth is merged into every method's requirements (ControllerType.AddMethodAsField)
    [GraphQLAuthorize("class-role")]
    internal class ClassLevelRolesMutations
    {
        [GraphQLMutation]
        public static bool ClassOnly() => true;

        [GraphQLAuthorize("method-role")]
        [GraphQLMutation]
        public static bool ClassAndMethod() => true;
    }
}
