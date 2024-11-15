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

        Assert.Single(schema.Type<Project>().RequiredAuthorization!.Roles);
        Assert.Equal("admin", schema.Type<Project>().RequiredAuthorization!.Roles.ElementAt(0).ElementAt(0));
    }

    [Fact]
    public void TestAttributeOnTypeAddType()
    {
        var schema = new SchemaProvider<object>();
        schema.AddType<Project>("Project", "All about the project");

        Assert.Single(schema.Type<Project>().RequiredAuthorization!.Roles);
        Assert.Equal("admin", schema.Type<Project>().RequiredAuthorization!.Roles.ElementAt(0).ElementAt(0));
    }

    [Fact]
    public void TestMethodOnType()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();

        Assert.Empty(schema.Type<Task>().RequiredAuthorization!.Roles);

        schema.Type<Task>().RequiresAnyRole("admin");

        Assert.Single(schema.Type<Task>().RequiredAuthorization!.Roles);
        Assert.Equal("admin", schema.Type<Task>().RequiredAuthorization!.Roles.ElementAt(0).ElementAt(0));
    }

    [Fact]
    public void TestAttributeOnField()
    {
        var schema = SchemaBuilder.FromObject<RolesDataContext>();

        Assert.Single(schema.Type<Project>().GetField("type", null).RequiredAuthorization!.Roles);
        Assert.Equal("can-type", schema.Type<Project>().GetField("type", null).RequiredAuthorization!.Roles.ElementAt(0).ElementAt(0));
    }

    [Fact]
    public void TestAttributeOnFieldAddField()
    {
        var schema = new SchemaProvider<object>();
        schema.AddType<Project>("Project", "All about the project").AddField(p => p.Type, "The type info");

        Assert.Single(schema.Type<Project>().GetField("type", null).RequiredAuthorization!.Roles);
        Assert.Equal("can-type", schema.Type<Project>().GetField("type", null).RequiredAuthorization!.Roles.ElementAt(0).ElementAt(0));
    }

    [Fact]
    public void TestMethodOnField()
    {
        var schema = new SchemaProvider<object>();

        schema.AddType<Task>("Task", "All about tasks").AddField(p => p.IsActive, "Is it active").RequiresAnyRole("admin");

        Assert.Single(schema.Type<Task>().GetField("isActive", null).RequiredAuthorization!.Roles);
        Assert.Equal("admin", schema.Type<Task>().GetField("isActive", null).RequiredAuthorization!.Roles.ElementAt(0).ElementAt(0));
    }

    [Fact]
    public void TestRequiresAnyRoleMany()
    {
        var schema = new SchemaProvider<object>();

        schema.AddType<Task>("Task", "All about tasks").AddField(p => p.IsActive, "Is it active").RequiresAnyRole("admin", "something-else");

        Assert.Single(schema.Type<Task>().GetField("isActive", null).RequiredAuthorization!.Roles);
        Assert.Equal("admin", schema.Type<Task>().GetField("isActive", null).RequiredAuthorization!.Roles.ElementAt(0).ElementAt(0));
        Assert.Equal("something-else", schema.Type<Task>().GetField("isActive", null).RequiredAuthorization!.Roles.ElementAt(0).ElementAt(1));
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
        Assert.Equal("Field 'needsAuth' - You are not authorized to access the 'needsAuth' field on type 'Mutation'.", result.Errors.First().Message);

        claims = new ClaimsIdentity([new Claim(ClaimTypes.Role, "can-mutate")], "authed");
        result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

        Assert.Null(result.Errors);
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
