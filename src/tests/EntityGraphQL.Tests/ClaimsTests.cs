using Xunit;
using System.Collections.Generic;
using EntityGraphQL.Schema;
using System.Linq;
using EntityGraphQL.Authorization;
using System.Security.Claims;

namespace EntityGraphQL.Tests
{
    public class ClaimsTests
    {
        [Fact]
        public void TestAttributeOnTypeFromObject()
        {
            var schema = SchemaBuilder.FromObject<ClaimsDataContext>();

            Assert.Single(schema.Type<Project>().AuthorizeClaims.Claims);
            Assert.Equal("admin", schema.Type<Project>().AuthorizeClaims.Claims.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestAttributeOnTypeAddType()
        {
            var schema = new SchemaProvider<object>();
            schema.AddType<Project>("Project", "All about the project");

            Assert.Single(schema.Type<Project>().AuthorizeClaims.Claims);
            Assert.Equal("admin", schema.Type<Project>().AuthorizeClaims.Claims.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestMethodOnType()
        {
            var schema = SchemaBuilder.FromObject<ClaimsDataContext>();

            Assert.Empty(schema.Type<Task>().AuthorizeClaims.Claims);

            schema.Type<Task>().RequiresAnyClaim("admin");

            Assert.Single(schema.Type<Task>().AuthorizeClaims.Claims);
            Assert.Equal("admin", schema.Type<Task>().AuthorizeClaims.Claims.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestAttributeOnField()
        {
            var schema = SchemaBuilder.FromObject<ClaimsDataContext>();

            Assert.Single(schema.Type<Project>().GetField("type").AuthorizeClaims.Claims);
            Assert.Equal("can-type", schema.Type<Project>().GetField("type").AuthorizeClaims.Claims.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestAttributeOnFieldAddField()
        {
            var schema = new SchemaProvider<object>();
            schema.AddType<Project>("Project", "All about the project")
            .AddField(p => p.Type, "The type info");

            Assert.Single(schema.Type<Project>().GetField("type").AuthorizeClaims.Claims);
            Assert.Equal("can-type", schema.Type<Project>().GetField("type").AuthorizeClaims.Claims.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestMethodOnField()
        {
            var schema = new SchemaProvider<object>();

            schema.AddType<Task>("Task", "All about tasks")
            .AddField(p => p.IsActive, "Is it active")
            .RequiresAnyClaim("admin");

            Assert.Single(schema.Type<Task>().GetField("isActive").AuthorizeClaims.Claims);
            Assert.Equal("admin", schema.Type<Task>().GetField("isActive").AuthorizeClaims.Claims.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestFieldIsSecured()
        {
            var schema = SchemaBuilder.FromObject<ClaimsDataContext>();

            var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin") }, "authed");
            QueryRequest gql = new QueryRequest
            {
                Query = @"{
                    projects { type }
                }"
            };
            var result = schema.ExecuteQuery(gql, new ClaimsDataContext(), null, claims);

            Assert.Equal("You do not have access to field 'type' on type 'Project'. You require any of the following security claims [can-type]", result.Errors.First().Message);

            claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin"), new Claim(ClaimTypes.Role, "can-type") }, "authed");
            result = schema.ExecuteQuery(gql, new ClaimsDataContext(), null, claims);

            Assert.Empty(result.Errors);
        }

        [Fact]
        public void TestTypeIsSecured()
        {
            var schema = SchemaBuilder.FromObject<ClaimsDataContext>();

            var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "not-admin") }, "authed");
            QueryRequest gql = new QueryRequest
            {
                Query = @"{
                    projects { id }
                }"
            };
            var result = schema.ExecuteQuery(gql, new ClaimsDataContext(), null, claims);

            Assert.Equal("You do not have access to the 'Project' type. You require any of the following security claims [admin]", result.Errors.First().Message);

            claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin") }, "authed");
            result = schema.ExecuteQuery(gql, new ClaimsDataContext(), null, claims);

            Assert.Empty(result.Errors);
        }

        [Fact]
        public void TestNonTopLevelTypeIsSecured()
        {
            var schema = SchemaBuilder.FromObject<ClaimsDataContext>();

            var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "not-admin") }, "authed");
            QueryRequest gql = new QueryRequest
            {
                Query = @"{
                    tasks {
                        project { id }
                    }
                }"
            };
            var result = schema.ExecuteQuery(gql, new ClaimsDataContext(), null, claims);

            Assert.Equal("You do not have access to the 'Project' type. You require any of the following security claims [admin]", result.Errors.First().Message);

            claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin") }, "authed");
            result = schema.ExecuteQuery(gql, new ClaimsDataContext(), null, claims);

            Assert.Empty(result.Errors);
        }

        internal class ClaimsDataContext
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
        }
    }
}