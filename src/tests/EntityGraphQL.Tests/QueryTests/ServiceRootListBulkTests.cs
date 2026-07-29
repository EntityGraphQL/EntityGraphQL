using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// Regression for ResolveBulk on service-resolved root lists with nullable string keys
/// (apiKeys-style: list from Resolve&lt;TService&gt;, nested bulk fields). These used to fail with
/// "Static property requires null instance..." reaching the client as "Field 'apiKeys' - Error occurred".
/// A bulk field below a root field that is resolved from services currently resolves per-item, not in bulk:
/// the first (no-service) pass produces no expression for the root field, so there is nothing to collect the
/// bulk keys from and no load is registered. Hence the one-call-per-item counts asserted below - they become
/// a single bulk call if root service lists ever take part in the two-pass flow.
/// Tests from https://github.com/EntityGraphQL/EntityGraphQL/pull/538
/// </summary>
public class ServiceRootListBulkTests
{
    public class ApiKey
    {
        public string Key { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string? CreatedById { get; set; }
    }

    public class Customer
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class EmptyContext { }

    public class ApiKeyService
    {
        public List<ApiKey> Keys { get; set; } = [];

        public List<ApiKey> GetApiKeys() => Keys;
    }

    public class CustomerService
    {
        public int CallCount { get; private set; }
        public List<Customer> Customers { get; set; } = [];

        public Customer? GetCustomer(Guid id)
        {
            CallCount++;
            return Customers.FirstOrDefault(c => c.Id == id);
        }

        public IDictionary<Guid, Customer> GetCustomers(IEnumerable<Guid> ids)
        {
            CallCount++;
            return Customers.Where(c => ids.Contains(c.Id)).ToDictionary(c => c.Id);
        }
    }

    public class CreatedByNameService
    {
        public int CallCount { get; private set; }
        public Dictionary<string, string> Names { get; set; } = [];

        public string? GetName(string? id)
        {
            CallCount++;
            if (string.IsNullOrEmpty(id))
                return null;
            return Names.TryGetValue(id, out var name) ? name : null;
        }

        public System.Threading.Tasks.Task<string?> GetNameAsync(string? id) => System.Threading.Tasks.Task.FromResult(GetName(id));

        public IDictionary<string, string?> GetNames(IEnumerable<string> ids)
        {
            CallCount++;
            // Bulk dictionary lookup uses the indexer — every selected key must be present (null value = unknown).
            return ids.Distinct().ToDictionary(id => id ?? string.Empty, id => string.IsNullOrEmpty(id) ? null : (Names.TryGetValue(id, out var name) ? name : null));
        }

        public System.Threading.Tasks.Task<IDictionary<string, string?>> GetNamesAsync(IEnumerable<string> ids) => System.Threading.Tasks.Task.FromResult(GetNames(ids));
    }

    private static (SchemaProvider<EmptyContext> schema, ServiceProvider sp, ApiKeyService apiKeys, CreatedByNameService names, CustomerService customers) BuildApiKeysSchema(
        Action<SchemaType<ApiKey>> configureApiKey
    )
    {
        var schema = SchemaBuilder.FromObject<EmptyContext>();
        schema.AddType<ApiKey>("ApiKey", "API key").AddAllFields();
        schema.AddType<Customer>("Customer", "Customer").AddAllFields();

        schema.Query().AddField("apiKeys", "List of API keys").Resolve<ApiKeyService>((_, svc) => svc.GetApiKeys());

        configureApiKey(schema.Type<ApiKey>());

        var customerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var customerB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var apiKeyService = new ApiKeyService
        {
            Keys =
            [
                new ApiKey
                {
                    Key = "key-1",
                    CustomerId = customerA,
                    CreatedById = "user-1",
                },
                new ApiKey
                {
                    Key = "key-2",
                    CustomerId = customerB,
                    CreatedById = "user-2",
                },
                new ApiKey
                {
                    Key = "key-3",
                    CustomerId = customerA,
                    CreatedById = "user-1",
                },
            ],
        };
        var nameService = new CreatedByNameService { Names = { ["user-1"] = "Alice", ["user-2"] = "Bob" } };
        var customerService = new CustomerService { Customers = [new Customer { Id = customerA, Name = "Acme" }, new Customer { Id = customerB, Name = "Beta" }] };

        var services = new ServiceCollection();
        services.AddSingleton(apiKeyService);
        services.AddSingleton(nameService);
        services.AddSingleton(customerService);
        services.AddSingleton(new EmptyContext());
        return (schema, services.BuildServiceProvider(), apiKeyService, nameService, customerService);
    }

    [Fact]
    public void ServiceRootList_BulkWithPlainStringKey_Works()
    {
        var (schema, sp, _, names, _) = BuildApiKeysSchema(apiKey =>
        {
            apiKey
                .AddField("createdByName", "Creator display name")
                .Resolve<CreatedByNameService>((k, svc) => svc.GetName(k.CreatedById))
                // Non-null keys only — keys with null CreatedById are filtered by the bulk loader
                .ResolveBulk<CreatedByNameService, string, string?>(k => k.CreatedById!, (ids, svc) => svc.GetNames(ids));
        });

        var res = schema.ExecuteRequest(
            new QueryRequest
            {
                Query =
                    @"{
                    apiKeys {
                        key
                        createdByName
                    }
                }",
            },
            sp,
            null
        );

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        Assert.Equal(3, names.CallCount); // per-item fallback - one call per API key
        dynamic keys = res.Data!["apiKeys"]!;
        Assert.Equal(3, keys.Count);
        Assert.Equal("Alice", keys[0].createdByName);
        Assert.Equal("Bob", keys[1].createdByName);
        Assert.Equal("Alice", keys[2].createdByName);
    }

    /// <summary>
    /// Key selector coalesces null CreatedById to string.Empty (static property).
    /// Includes a null CreatedById row — the bulk loader must return an entry for "" so the
    /// dictionary indexer does not throw.
    /// </summary>
    [Fact]
    public void ServiceRootList_BulkWithStringEmptyCoalesce_Works()
    {
        var (schema, sp, apiKeys, names, _) = BuildApiKeysSchema(apiKey =>
        {
            apiKey
                .AddField("createdByName", "Creator display name")
                .Resolve<CreatedByNameService>((k, svc) => svc.GetName(k.CreatedById))
                .ResolveBulk<CreatedByNameService, string, string?>(k => k.CreatedById ?? string.Empty, (ids, svc) => svc.GetNames(ids));
        });
        apiKeys.Keys[1].CreatedById = null;

        var res = schema.ExecuteRequest(
            new QueryRequest
            {
                Query =
                    @"{
                    apiKeys {
                        key
                        createdByName
                    }
                }",
            },
            sp,
            null
        );

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        Assert.Equal(3, names.CallCount); // per-item fallback - one call per API key
        dynamic keys = res.Data!["apiKeys"]!;
        Assert.Equal(3, keys.Count);
        Assert.Equal("Alice", keys[0].createdByName);
        Assert.Null(keys[1].createdByName);
        Assert.Equal("Alice", keys[2].createdByName);
    }

    /// <summary>
    /// Same coalesce using a string literal instead of string.Empty.
    /// </summary>
    [Fact]
    public void ServiceRootList_BulkWithEmptyLiteralCoalesce_Works()
    {
        var (schema, sp, apiKeys, names, _) = BuildApiKeysSchema(apiKey =>
        {
            apiKey
                .AddField("createdByName", "Creator display name")
                .Resolve<CreatedByNameService>((k, svc) => svc.GetName(k.CreatedById))
                .ResolveBulk<CreatedByNameService, string, string?>(k => k.CreatedById ?? "", (ids, svc) => svc.GetNames(ids));
        });
        apiKeys.Keys[1].CreatedById = null;

        var res = schema.ExecuteRequest(
            new QueryRequest
            {
                Query =
                    @"{
                    apiKeys {
                        key
                        createdByName
                    }
                }",
            },
            sp,
            null
        );

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        Assert.Equal(3, names.CallCount); // per-item fallback - one call per API key
        dynamic keys = res.Data!["apiKeys"]!;
        Assert.Equal("Alice", keys[0].createdByName);
        Assert.Null(keys[1].createdByName);
    }

    [Fact]
    public void ServiceRootList_BulkCustomerLookup_Works()
    {
        var (schema, sp, _, _, customers) = BuildApiKeysSchema(apiKey =>
        {
            apiKey
                .AddField("customer", "Owning customer")
                .Resolve<CustomerService>((k, svc) => svc.GetCustomer(k.CustomerId))
                .ResolveBulk<CustomerService, Guid, Customer>(k => k.CustomerId, (ids, svc) => svc.GetCustomers(ids));
        });

        var res = schema.ExecuteRequest(
            new QueryRequest
            {
                Query =
                    @"{
                    apiKeys {
                        key
                        customer { id name }
                    }
                }",
            },
            sp,
            null
        );

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        Assert.Equal(3, customers.CallCount); // per-item fallback - one call per API key
        dynamic keys = res.Data!["apiKeys"]!;
        Assert.Equal("Acme", keys[0].customer.name);
        Assert.Equal("Beta", keys[1].customer.name);
        Assert.Equal("Acme", keys[2].customer.name);
    }

    /// <summary>
    /// Async bulk via ResolveBulkAsync (native) on a service-resolved root list — preferred over
    /// closure-capturing sync wrappers around per-key async callbacks.
    /// Skipped on a separate, pre-existing bug that has nothing to do with bulk: any ResolveAsync field
    /// below a service-resolved root list is not awaited, so the response holds the Task&lt;T&gt; instead of
    /// its result (the same field below a context root list is awaited, in one pass or two). Un-skip when
    /// that is fixed - with the per-item fallback the assertion is 3 calls, one per API key.
    /// </summary>
    [Fact(Skip = "ResolveAsync field below a service-resolved root list is not awaited - unrelated to bulk")]
    public void ServiceRootList_BulkAsyncCreatedByName_Works()
    {
        var (schema, sp, _, names, _) = BuildApiKeysSchema(apiKey =>
        {
            apiKey
                .AddField("createdByName", "Creator display name")
                .ResolveAsync<CreatedByNameService>((k, svc) => svc.GetNameAsync(k.CreatedById))
                .ResolveBulkAsync<CreatedByNameService, string, string?>(k => k.CreatedById ?? "", (ids, svc) => svc.GetNamesAsync(ids));
        });

        var res = schema.ExecuteRequest(
            new QueryRequest
            {
                Query =
                    @"{
                    apiKeys {
                        key
                        createdByName
                    }
                }",
            },
            sp,
            null
        );

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        Assert.Equal(3, names.CallCount); // per-item fallback - one call per API key
        dynamic keys = res.Data!["apiKeys"]!;
        Assert.Equal("Alice", keys[0].createdByName);
        Assert.Equal("Bob", keys[1].createdByName);
        Assert.Equal("Alice", keys[2].createdByName);
    }
}
