using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// Regression for ResolveBulk on service-resolved root lists with nullable string keys
/// (apiKeys-style: list from Resolve&lt;TService&gt;, nested bulk fields).
/// </summary>
public class ServiceRootListBulkTests
{
    public class ApiKey
    {
        public string Key { get; set; } = string.Empty;
        public Guid CustomerId { get; set; }
        public string? CreatedById { get; set; }
        public Board? Board { get; set; }
    }

    public class Board
    {
        public string SerialNumber { get; set; } = string.Empty;
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

        public StatusPage GetStatusPage() => new() { TotalItems = Keys.Count, Items = Keys };
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
        Assert.Equal(1, names.CallCount);
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
        Assert.Equal(1, names.CallCount);
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
        Assert.Equal(1, names.CallCount);
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
        Assert.Equal(1, customers.CallCount);
        dynamic keys = res.Data!["apiKeys"]!;
        Assert.Equal("Acme", keys[0].customer.name);
        Assert.Equal("Beta", keys[1].customer.name);
        Assert.Equal("Acme", keys[2].customer.name);
    }

    /// <summary>
    /// Async bulk via ResolveBulkAsync (native) on a service-resolved root list — preferred over
    /// closure-capturing sync wrappers around per-key async callbacks.
    /// </summary>
    [Fact]
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
        Assert.Equal(1, names.CallCount);
        dynamic keys = res.Data!["apiKeys"]!;
        Assert.Equal("Alice", keys[0].createdByName);
        Assert.Equal("Bob", keys[1].createdByName);
        Assert.Equal("Alice", keys[2].createdByName);
    }

    /// <summary>
    /// Status-page shape: root Resolve returns a page object with a materialized Items list,
    /// and items have nested ResolveBulk fields. Must not fail looking up egql__* on the entity type.
    /// </summary>
    public class StatusPage
    {
        public int TotalItems { get; set; }
        public List<ApiKey> Items { get; set; } = [];
    }

    [Fact]
    public void ServiceResolvedPage_ItemsWithResolveBulk_Works()
    {
        var schema = SchemaBuilder.FromObject<EmptyContext>();
        schema.AddType<ApiKey>("ApiKey", "API key").AddAllFields();
        schema.AddType<StatusPage>("StatusPage", "Paged API keys").AddAllFields();

        schema.Query().AddField("statusPage", "Paged API keys from a service").Resolve<ApiKeyService>((_, svc) => svc.GetStatusPage());

        schema
            .Type<ApiKey>()
            .AddField("createdByName", "Creator display name")
            .Resolve<CreatedByNameService>((k, svc) => svc.GetName(k.CreatedById))
            .ResolveBulk<CreatedByNameService, string, string?>(k => k.CreatedById ?? string.Empty, (ids, svc) => svc.GetNames(ids));

        var customerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
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
                    CustomerId = customerA,
                    CreatedById = "user-2",
                },
            ],
        };
        var nameService = new CreatedByNameService { Names = { ["user-1"] = "Alice", ["user-2"] = "Bob" } };

        var services = new ServiceCollection();
        services.AddSingleton(new EmptyContext());
        services.AddSingleton(apiKeyService);
        services.AddSingleton(nameService);
        var sp = services.BuildServiceProvider();

        var res = schema.ExecuteRequest(
            new QueryRequest
            {
                Query =
                    @"{
                    statusPage {
                        totalItems
                        items {
                            key
                            createdByName
                        }
                    }
                }",
            },
            sp,
            null
        );

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => $"{e.Message} path=[{string.Join('.', e.Path ?? [])}]")));
        Assert.Equal(1, nameService.CallCount);
        dynamic page = res.Data!["statusPage"]!;
        Assert.Equal(2, page.totalItems);
        Assert.Equal("Alice", page.items[0].createdByName);
        Assert.Equal("Bob", page.items[1].createdByName);
    }

    /// <summary>
    /// Same page shape with a multi-property data selector (HardwareSensorInventory-style Key(...)),
    /// which previously left the selector parameter unbound on the second pass.
    /// </summary>
    public static class ApiKeyBulkKeys
    {
        public static string CompositeKey(string key, string? createdById, Guid customerId) => string.Join('\u001f', key, createdById ?? string.Empty, customerId.ToString("D"));
    }

    public class CreatedByNameByCompositeKeyService
    {
        public int CallCount { get; private set; }
        public Dictionary<string, string> Names { get; set; } = [];

        public string? GetName(string? createdById)
        {
            CallCount++;
            if (string.IsNullOrEmpty(createdById))
                return null;
            return Names.TryGetValue(createdById, out var name) ? name : null;
        }

        public IDictionary<string, string?> GetNamesByCompositeKeys(IEnumerable<string> keys)
        {
            CallCount++;
            return keys.Distinct()
                .ToDictionary(
                    id => id,
                    id =>
                    {
                        var parts = id.Split('\u001f');
                        var createdBy = parts.Length > 1 ? parts[1] : string.Empty;
                        if (string.IsNullOrEmpty(createdBy))
                            return null;
                        return Names.TryGetValue(createdBy, out var name) ? name : null;
                    }
                );
        }
    }

    [Fact]
    public void ServiceResolvedPage_ItemsWithComplexResolveBulkKey_Works()
    {
        var schema = SchemaBuilder.FromObject<EmptyContext>();
        schema.AddType<ApiKey>("ApiKey", "API key").AddAllFields();
        schema.AddType<StatusPage>("StatusPage", "Paged API keys").AddAllFields();

        schema.Query().AddField("statusPage", "Paged API keys from a service").Resolve<ApiKeyService>((_, svc) => svc.GetStatusPage());

        schema
            .Type<ApiKey>()
            .AddField("createdByName", "Creator display name")
            .Resolve<CreatedByNameByCompositeKeyService>((row, svc) => svc.GetName(row.CreatedById))
            .ResolveBulk<CreatedByNameByCompositeKeyService, string, string?>(
                row => ApiKeyBulkKeys.CompositeKey(row.Key, row.CreatedById, row.CustomerId),
                (ids, svc) => svc.GetNamesByCompositeKeys(ids)
            );

        var customerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
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
                    CustomerId = customerA,
                    CreatedById = "user-2",
                },
            ],
        };
        var nameService = new CreatedByNameByCompositeKeyService { Names = { ["user-1"] = "Alice", ["user-2"] = "Bob" } };

        var services = new ServiceCollection();
        services.AddSingleton(new EmptyContext());
        services.AddSingleton(apiKeyService);
        services.AddSingleton(nameService);
        var sp = services.BuildServiceProvider();

        var res = schema.ExecuteRequest(
            new QueryRequest
            {
                Query =
                    @"{
                    statusPage {
                        totalItems
                        items {
                            key
                            createdByName
                        }
                    }
                }",
            },
            sp,
            null
        );

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => $"{e.Message} path=[{string.Join('.', e.Path ?? [])}]")));
        Assert.Equal(1, nameService.CallCount);
        dynamic page = res.Data!["statusPage"]!;
        Assert.Equal("Alice", page.items[0].createdByName);
        Assert.Equal("Bob", page.items[1].createdByName);
    }
    /// <summary>
    /// The two-pass flow exists so a bulk resolver can collect its keys from the materialized rows. A
    /// selection with nothing to bulk load stays on the single-pass path - the extra pass would be an extra
    /// compile and a projection of every row for no gain.
    /// </summary>
    [Fact]
    public void ServiceRootList_WithoutBulkFieldSelected_RunsInOnePass()
    {
        var (schema, sp, _, names, _) = BuildApiKeysSchema(apiKey =>
        {
            apiKey
                .AddField("createdByName", "Creator display name")
                .Resolve<CreatedByNameService>((k, svc) => svc.GetName(k.CreatedById))
                .ResolveBulk<CreatedByNameService, string, string?>(k => k.CreatedById!, (ids, svc) => svc.GetNames(ids));
        });

        var passes = ExecuteCountingPasses(schema, sp, "{ apiKeys { key customerId } }");

        Assert.Equal([true], passes);
        Assert.Equal(0, names.CallCount);
    }

    /// <summary>
    /// Selecting the bulk field opts the root service list into the two passes - one load for all rows.
    /// </summary>
    [Fact]
    public void ServiceRootList_WithBulkFieldSelected_RunsTwoPasses()
    {
        var (schema, sp, _, names, _) = BuildApiKeysSchema(apiKey =>
        {
            apiKey
                .AddField("createdByName", "Creator display name")
                .Resolve<CreatedByNameService>((k, svc) => svc.GetName(k.CreatedById))
                .ResolveBulk<CreatedByNameService, string, string?>(k => k.CreatedById!, (ids, svc) => svc.GetNames(ids));
        });

        var passes = ExecuteCountingPasses(schema, sp, "{ apiKeys { key createdByName } }");

        Assert.Equal([false, true], passes);
        Assert.Equal(1, names.CallCount);
    }

    /// <summary>
    /// The bulk field reached through a fragment spread counts too - clients hoist selections into fragments,
    /// and a shape check that did not look inside them would quietly drop back to one call per row.
    /// </summary>
    [Fact]
    public void ServiceRootList_BulkFieldInFragment_StillLoadsOnce()
    {
        var (schema, sp, _, names, _) = BuildApiKeysSchema(apiKey =>
        {
            apiKey
                .AddField("createdByName", "Creator display name")
                .Resolve<CreatedByNameService>((k, svc) => svc.GetName(k.CreatedById))
                .ResolveBulk<CreatedByNameService, string, string?>(k => k.CreatedById!, (ids, svc) => svc.GetNames(ids));
        });

        var res = schema.ExecuteRequest(
            new QueryRequest { Query = "query { apiKeys { ...KeyFields } } fragment KeyFields on ApiKey { key createdByName }" },
            sp,
            null
        );

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        Assert.Equal(1, names.CallCount);
        dynamic keys = res.Data!["apiKeys"]!;
        Assert.Equal("Alice", keys[0].createdByName);
    }

    /// <summary>The isFinal flag of each execution the engine runs for a query.</summary>
    private static List<bool> ExecuteCountingPasses(SchemaProvider<EmptyContext> schema, ServiceProvider sp, string query)
    {
        List<bool> passes = [];
        var res = schema.ExecuteRequest(
            new QueryRequest { Query = query },
            sp,
            null,
            new ExecutionOptions
            {
                BeforeExecuting = (Expression expression, bool isFinal) =>
                {
                    passes.Add(isFinal);
                    return expression;
                },
            }
        );
        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => e.Message)));
        return passes;
    }

}
