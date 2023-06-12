using Xunit;
using System.Collections.Generic;
using EntityGraphQL.Schema;
using System.Linq;
using EntityGraphQL.Authorization;
using System.Security.Claims;

namespace EntityGraphQL.Tests
{
    public class RoleAuthorizationTests
    {
        [Fact]
        public void TestAttributeOnTypeFromObject()
        {
            var schema = SchemaBuilder.FromObject<RolesDataContext>();

            Assert.Single(schema.Type<Project>().RequiredAuthorization.Roles);
            Assert.Equal("admin", schema.Type<Project>().RequiredAuthorization.Roles.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestAttributeOnTypeAddType()
        {
            var schema = new SchemaProvider<object>();
            schema.AddType<Project>("Project", "All about the project");

            Assert.Single(schema.Type<Project>().RequiredAuthorization.Roles);
            Assert.Equal("admin", schema.Type<Project>().RequiredAuthorization.Roles.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestMethodOnType()
        {
            var schema = SchemaBuilder.FromObject<RolesDataContext>();

            Assert.Empty(schema.Type<Task>().RequiredAuthorization.Roles);

            schema.Type<Task>().RequiresAnyRole("admin");

            Assert.Single(schema.Type<Task>().RequiredAuthorization.Roles);
            Assert.Equal("admin", schema.Type<Task>().RequiredAuthorization.Roles.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestAttributeOnField()
        {
            var schema = SchemaBuilder.FromObject<RolesDataContext>();

            Assert.Single(schema.Type<Project>().GetField("type", null).RequiredAuthorization.Roles);
            Assert.Equal("can-type", schema.Type<Project>().GetField("type", null).RequiredAuthorization.Roles.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestAttributeOnFieldAddField()
        {
            var schema = new SchemaProvider<object>();
            schema.AddType<Project>("Project", "All about the project")
            .AddField(p => p.Type, "The type info");

            Assert.Single(schema.Type<Project>().GetField("type", null).RequiredAuthorization.Roles);
            Assert.Equal("can-type", schema.Type<Project>().GetField("type", null).RequiredAuthorization.Roles.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestMethodOnField()
        {
            var schema = new SchemaProvider<object>();

            schema.AddType<Task>("Task", "All about tasks")
            .AddField(p => p.IsActive, "Is it active")
            .RequiresAnyRole("admin");

            Assert.Single(schema.Type<Task>().GetField("isActive", null).RequiredAuthorization.Roles);
            Assert.Equal("admin", schema.Type<Task>().GetField("isActive", null).RequiredAuthorization.Roles.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestFieldIsSecured()
        {
            var schema = SchemaBuilder.FromObject<RolesDataContext>();

            var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin") }, "authed");
            var gql = new QueryRequest
            {
                Query = @"{
                    projects { type }
                }"
            };

            var result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

            Assert.Equal("You are not authorized to access the 'type' field on type 'Project'.", result.Errors.First().Message);

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
                Query = @"{
                    projects { id }
                }"
            };

            var result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

            Assert.Equal("You are not authorized to access the 'Project' type returned by field 'projects'.", result.Errors.First().Message);

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
                Query = @"{
                    projects { id }
                }"
            };

            var result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, null);

            Assert.NotNull(result.Errors);
            Assert.Equal("You are not authorized to access the 'Project' type returned by field 'projects'.", result.Errors.First().Message);
        }

        [Fact]
        public void TestNonTopLevelTypeIsSecured()
        {
            var schema = SchemaBuilder.FromObject<RolesDataContext>();

            var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "not-admin") }, "authed");
            var gql = new QueryRequest
            {
                Query = @"{
                    tasks {
                        project { id }
                    }
                }"
            };

            var result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

            Assert.Equal("You are not authorized to access the 'Project' type returned by field 'project'.", result.Errors.First().Message);

            claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin") }, "authed");
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
                Query = @"{
                    tasks {
                        id description
                    }
                }"
            };

            var result = schema.ExecuteRequestWithContext(gql, new RolesDataContext(), null, new ClaimsPrincipal(claims));

            Assert.Equal("You are not authorized to access the 'description' field on type 'Task'.", result.Errors.First().Message);

            claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "can-description") }, "authed");
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
            public IEnumerable<Task> Tasks { get; set; }
        }

        internal class Task
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public bool IsActive { get; set; }
            public Project Project { get; set; }

            [GraphQLAuthorize("can-description")]
            [GraphQLField("description")]
            public string GetDescription()
            {
                return "This is a description";
            }
        }
    }
}