using Xunit;
using EntityGraphQL.Schema;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.AspNet.Tests
{
    public class PoliciesTests
    {
        [Fact]
        public void TestAttributeOnTypeFromObject()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IAuthorizationService, DummyAuthService>();
            var services = serviceCollection.BuildServiceProvider();
            var schema = SchemaBuilder.FromObject<PolicyDataContext>(new SchemaBuilderSchemaOptions
            {
                AuthorizationService = new PolicyOrRoleBasedAuthorization(services.GetService<IAuthorizationService>()!),
                PreBuildSchemaFromContext = (context) =>
                {
                    context.AddAttributeHandler(new AuthorizeAttributeHandler());
                }
            });
            Assert.Single(schema.Type<Project>().RequiredAuthorization!.Policies);
            Assert.Equal("admin", schema.Type<Project>().RequiredAuthorization!.Policies.ElementAt(0).ElementAt(0));

            var sdl = schema.ToGraphQLSchemaString();
            Assert.Contains("type Project @authorize(roles: \"\", policies: \"admin\") {", sdl);
            Assert.Contains("type: Int! @authorize(roles: \"\", policies: \"can-type\")", sdl);
        }

        [Fact]
        public void TestAttributeOnTypeAddType()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IAuthorizationService, DummyAuthService>();
            var services = serviceCollection.BuildServiceProvider();

            var schema = new SchemaProvider<PolicyDataContext>(new PolicyOrRoleBasedAuthorization(services.GetService<IAuthorizationService>()!));
            schema.AddType<Project>("Project");

            Assert.Single(schema.Type<Project>().RequiredAuthorization!.Policies);
            Assert.Equal("admin", schema.Type<Project>().RequiredAuthorization!.Policies.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestMethodOnType()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IAuthorizationService, DummyAuthService>();
            var services = serviceCollection.BuildServiceProvider();

            var schema = SchemaBuilder.FromObject<PolicyDataContext>(new SchemaBuilderSchemaOptions { AuthorizationService = new PolicyOrRoleBasedAuthorization(services.GetService<IAuthorizationService>()!) });

            Assert.Empty(schema.Type<Task>().RequiredAuthorization!.Policies);

            schema.Type<Task>().RequiresAnyPolicy("admin");

            Assert.Single(schema.Type<Task>().RequiredAuthorization!.Policies);
            Assert.Equal("admin", schema.Type<Task>().RequiredAuthorization!.Policies.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestAttributeOnField()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IAuthorizationService, DummyAuthService>();
            var services = serviceCollection.BuildServiceProvider();

            var schema = SchemaBuilder.FromObject<PolicyDataContext>(new SchemaBuilderSchemaOptions { AuthorizationService = new PolicyOrRoleBasedAuthorization(services.GetService<IAuthorizationService>()!) });

            Assert.Single(schema.Type<Project>().GetField("type", null).RequiredAuthorization!.Policies);
            Assert.Equal("can-type", schema.Type<Project>().GetField("type", null).RequiredAuthorization!.Policies.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestAttributeOnFieldAddField()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IAuthorizationService, DummyAuthService>();
            var services = serviceCollection.BuildServiceProvider();

            var schema = new SchemaProvider<PolicyDataContext>(new PolicyOrRoleBasedAuthorization(services.GetService<IAuthorizationService>()!));

            schema.AddType<Project>("Project", "All about the project")
            .AddField(p => p.Type, "The type info");

            Assert.Single(schema.Type<Project>().GetField("type", null).RequiredAuthorization!.Policies);
            Assert.Equal("can-type", schema.Type<Project>().GetField("type", null).RequiredAuthorization!.Policies.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestMethodOnField()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IAuthorizationService, DummyAuthService>();
            var services = serviceCollection.BuildServiceProvider();

            var schema = new SchemaProvider<PolicyDataContext>(new PolicyOrRoleBasedAuthorization(services.GetService<IAuthorizationService>()!));

            schema.AddType<Task>("Task", "All about tasks")
            .AddField(p => p.IsActive, "Is it active")
            .RequiresAllPolicies("admin");

            Assert.Single(schema.Type<Task>().GetField("isActive", null).RequiredAuthorization!.Policies);
            Assert.Equal("admin", schema.Type<Task>().GetField("isActive", null).RequiredAuthorization!.Policies.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestMethodAllOnField()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<IAuthorizationService, DummyAuthService>();
            var services = serviceCollection.BuildServiceProvider();

            var schema = new SchemaProvider<PolicyDataContext>(new PolicyOrRoleBasedAuthorization(services.GetService<IAuthorizationService>()!));

            schema.AddType<Task>("Task", "All about tasks")
                .AddField(p => p.IsActive, "Is it active")
                .RequiresAllPolicies("admin", "can-type");

            Assert.Equal(2, schema.Type<Task>().GetField("isActive", null).RequiredAuthorization!.Policies.Count());
            Assert.Equal("admin", schema.Type<Task>().GetField("isActive", null).RequiredAuthorization!.Policies.ElementAt(0).ElementAt(0));
        }

        [Fact]
        public void TestFieldIsSecured()
        {
            var serviceCollection = new ServiceCollection();
            static bool adminPolicy(ClaimsPrincipal user) => user.IsInRole("admin");
            static bool canType(ClaimsPrincipal user) => user.IsInRole("can-type");
            serviceCollection.AddSingleton<IAuthorizationService>(new DummyAuthService(new Dictionary<string, Func<ClaimsPrincipal, bool>> { { "admin", adminPolicy }, { "can-type", canType } }));
            var services = serviceCollection.BuildServiceProvider();

            var schema = SchemaBuilder.FromObject<PolicyDataContext>(new SchemaBuilderSchemaOptions { AuthorizationService = new PolicyOrRoleBasedAuthorization(services.GetService<IAuthorizationService>()!) });

            var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin") }, "authed");
            var gql = new QueryRequest
            {
                Query = @"{
                    projects { type }
                }"
            };

            var result = schema.ExecuteRequestWithContext(gql, new PolicyDataContext(), services, new ClaimsPrincipal(claims));

            Assert.Equal("You are not authorized to access the 'type' field on type 'Project'.", result.Errors!.First().Message);

            claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin"), new Claim(ClaimTypes.Role, "can-type") }, "authed");
            result = schema.ExecuteRequestWithContext(gql, new PolicyDataContext(), services, new ClaimsPrincipal(claims));

            Assert.Null(result.Errors);
        }

        [Fact]
        public void TestTypeIsSecured()
        {
            var serviceCollection = new ServiceCollection();
            static bool adminPolicy(ClaimsPrincipal user) => user.IsInRole("admin");
            serviceCollection.AddSingleton<IAuthorizationService>(new DummyAuthService(new Dictionary<string, Func<ClaimsPrincipal, bool>> { { "admin", adminPolicy } }));
            var services = serviceCollection.BuildServiceProvider();

            var schema = SchemaBuilder.FromObject<PolicyDataContext>(new SchemaBuilderSchemaOptions { AuthorizationService = new PolicyOrRoleBasedAuthorization(services.GetService<IAuthorizationService>()!) });

            var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "not-admin") }, "authed");
            var gql = new QueryRequest
            {
                Query = @"{
                    projects { id }
                }"
            };

            var result = schema.ExecuteRequestWithContext(gql, new PolicyDataContext(), services, new ClaimsPrincipal(claims));

            Assert.Equal("You are not authorized to access the 'Project' type returned by field 'projects'.", result.Errors!.First().Message);

            claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin") }, "authed");
            result = schema.ExecuteRequestWithContext(gql, new PolicyDataContext(), services, new ClaimsPrincipal(claims));

            Assert.Null(result.Errors);
        }

        [Fact]
        public void TestNonTopLevelTypeIsSecured()
        {
            var serviceCollection = new ServiceCollection();
            static bool adminPolicy(ClaimsPrincipal user) => user.IsInRole("admin");
            serviceCollection.AddSingleton<IAuthorizationService>(new DummyAuthService(new Dictionary<string, Func<ClaimsPrincipal, bool>> { { "admin", adminPolicy } }));
            var services = serviceCollection.BuildServiceProvider();

            var schema = SchemaBuilder.FromObject<PolicyDataContext>(new SchemaBuilderSchemaOptions { AuthorizationService = new PolicyOrRoleBasedAuthorization(services.GetService<IAuthorizationService>()!) });

            var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "not-admin") }, "authed");
            var gql = new QueryRequest
            {
                Query = @"{
                    tasks {
                        project { id }
                    }
                }"
            };

            var result = schema.ExecuteRequestWithContext(gql, new PolicyDataContext(), services, new ClaimsPrincipal(claims));

            Assert.Equal("You are not authorized to access the 'Project' type returned by field 'project'.", result.Errors!.First().Message);

            claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin") }, "authed");
            result = schema.ExecuteRequestWithContext(gql, new PolicyDataContext(), services, new ClaimsPrincipal(claims));

            Assert.Null(result.Errors);
        }

        internal class PolicyDataContext
        {
            public IEnumerable<Project> Projects { get; set; } = new List<Project>();
            public IEnumerable<Task> Tasks { get; set; } = new List<Task>();
        }
        [Authorize("admin")]
        internal class Project
        {
            public int Id { get; set; }
            [GraphQLAuthorizePolicy("can-type")]
            public int Type { get; set; }
            public IEnumerable<Task>? Tasks { get; set; }
        }

        internal class Task
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public bool IsActive { get; set; }
            public Project? Project { get; set; }
        }
    }

    internal class DummyAuthService : IAuthorizationService
    {
        private readonly Dictionary<string, Func<ClaimsPrincipal, bool>>? policies;

        public DummyAuthService()
        { }

        public DummyAuthService(Dictionary<string, Func<ClaimsPrincipal, bool>> policies)
        {
            this.policies = policies;
        }
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            throw new NotImplementedException();
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
        {
            if (policies == null)
                return Task.FromResult(AuthorizationResult.Failed());
            if (!policies.ContainsKey(policyName))
                return Task.FromResult(AuthorizationResult.Failed());

            return policies[policyName](user) ? Task.FromResult(AuthorizationResult.Success()) : Task.FromResult(AuthorizationResult.Failed());
        }
    }
}