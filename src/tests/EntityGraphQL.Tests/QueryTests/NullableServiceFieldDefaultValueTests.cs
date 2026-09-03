using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// A nullable value type must stay null when the results are rebuilt to await async fields. The rebuild takes
/// each member's new type from the resolved value's runtime type, and a boxed Nullable&lt;T&gt; reports T, so the
/// member used to come out non-nullable - and because that type is reused for every row, a null in a later row
/// was written as default(T).
/// The shape here is the one that found it, from a real schema: a root list field taking arguments, a
/// bulk-resolved nullable field, a second bulk-resolved field returning a list, and a ResolveAsync field taking
/// arguments (so it has no bulk resolver, and its value really is a Task - that is what triggers the rebuild).
/// It needs one alert with a value and one without: the first types the member, the second gets the default.
/// </summary>
public class NullableServiceFieldDefaultValueTests
{
    /// <summary>Stands in for NodaTime's Instant - a value type behind a scalar.</summary>
    public struct Instant
    {
        public long Ticks { get; set; }

        public override string ToString() => $"Instant(Ticks={Ticks})";
    }

    public class Alert
    {
        public Guid Id { get; set; }
        public Guid SpaceId { get; set; }
    }

    public class AlertEntityIdName
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class AlertsContext
    {
        public IEnumerable<Alert> Alerts { get; set; } = [];
    }

    /// <summary>
    /// Nothing here returns a value. The bulk loaders put an entry in for every requested id, mapped to null -
    /// EntityGraphQL's bulk dictionary indexer throws on a missing key, so every key has to be present.
    /// </summary>
    public class AlertsProvider
    {
        public System.Threading.Tasks.Task<Instant?> LastTriggeredAsync(Alert alert) => System.Threading.Tasks.Task.FromResult<Instant?>(null);

        /// <summary>Only NeverTriggeredId has never triggered - every other alert has a real value.</summary>
        public System.Threading.Tasks.Task<IDictionary<Guid, Instant?>> LastTriggeredManyAsync(IEnumerable<Guid> alertIds) =>
            System.Threading.Tasks.Task.FromResult<IDictionary<Guid, Instant?>>(
                alertIds.Distinct().ToDictionary(id => id, id => id == NeverTriggeredId ? (Instant?)null : new Instant { Ticks = 123 })
            );

        public System.Threading.Tasks.Task<Instant?> LastTriggeredForEntity(Alert alert, Guid? entityId) => System.Threading.Tasks.Task.FromResult<Instant?>(null);

        public List<AlertEntityIdName> GetEntityNames(Alert alert) => [new AlertEntityIdName { Id = alert.SpaceId, Name = "Space" }];

        public IDictionary<Guid, List<AlertEntityIdName>> GetEntityNamesMany(IEnumerable<Guid> alertIds) =>
            alertIds
                .Distinct()
                .ToDictionary(
                    id => id,
                    id => new List<AlertEntityIdName>
                    {
                        new() { Id = id, Name = "Space" },
                    }
                );
    }

    /// <summary>The alert with no last-triggered value - it must come back null, not default(Instant).</summary>
    private static readonly Guid NeverTriggeredId = new("c8f375bc-f14b-4f45-8da2-874342cf0ff8");
    private static readonly Guid AlertId = NeverTriggeredId;
    private static readonly Guid SpaceId = new("11111111-2222-3333-4444-555555555555");

    private static object? LastTriggeredOfFirstAlert(string query, string resultField = "alerts")
    {
        var schema = SchemaBuilder.FromObject<AlertsContext>();
        schema.AddScalarType<Instant>("Date", "Instant in time");
        schema.AddTypeMapping<Instant>("Date!");
        schema.AddTypeMapping<Instant?>("Date");
        schema.AddType<AlertEntityIdName>("AlertEntityIdName", "Entity id and name").AddAllFields();

        schema.Query().ReplaceField("alerts", new { spaceId = (Guid?)null }, (ctx, p) => ctx.Alerts.Where(a => !p.spaceId.HasValue || a.SpaceId == p.spaceId.Value), "Retrieves alerts");
        schema.Query().ReplaceField("alert", new { id = (Guid?)null }, (ctx, p) => ctx.Alerts.FirstOrDefault(a => a.Id == p.id), "Retrieves one alert");

        schema.UpdateType<Alert>(a =>
        {
            a.AddField("entityNames", "Names of entities being watched")
                .Resolve<AlertsProvider>((alert, ap) => ap.GetEntityNames(alert))
                .ResolveBulk<AlertsProvider, Guid, List<AlertEntityIdName>>(alert => alert.Id, (alertIds, ap) => ap.GetEntityNamesMany(alertIds));
            a.AddField("lastTriggered", "When the alert was last triggered for any entity")
                .ResolveAsync<AlertsProvider>((alert, ap) => ap.LastTriggeredAsync(alert))
                .ResolveBulkAsync<AlertsProvider, Guid, Instant?>(alert => alert.Id, (alertIds, ap) => ap.LastTriggeredManyAsync(alertIds));
            a.AddField("lastTriggeredForEntity", new { entityId = (Guid?)null }, "When the alert was last triggered for a specific entity")
                .ResolveAsync<AlertsProvider>((alert, args, ap) => ap.LastTriggeredForEntity(alert, args.entityId));
        });

        // many rows: the async non-bulk field resolves them concurrently
        // an alert that HAS a value comes first, so the resolution plan is built from a non-null value
        var alerts = new List<Alert>
        {
            new() { Id = Guid.NewGuid(), SpaceId = SpaceId },
            new() { Id = NeverTriggeredId, SpaceId = SpaceId },
        };
        var context = new AlertsContext { Alerts = alerts };
        var services = new ServiceCollection().AddSingleton(new AlertsProvider()).BuildServiceProvider();

        var gql = new QueryRequest
        {
            Query = query,
            Variables = new QueryVariables { { "spaceId", SpaceId } },
        };
        var res = schema.ExecuteRequestWithContext(gql, context, services, null);
        Assert.Null(res.Errors);
        var data = (dynamic)res.Data![resultField]!;
        if (resultField != "alerts")
            return data.lastTriggered;
        // any row with a value instead of null is the bug - report the first
        foreach (var row in data)
        {
            if (row.id == NeverTriggeredId)
                return row.lastTriggered;
        }
        throw new InvalidOperationException("alert not in the result");
    }

    [Fact]
    public void BulkNullableIsNull_WithArgumentFieldAndBulkListField()
    {
        var lastTriggered = LastTriggeredOfFirstAlert(
            @"query GetAlerts_Broken($spaceId: ID) {
                alerts(spaceId: $spaceId) {
                    id
                    lastTriggered
                    lastTriggeredForEntity(entityId: $spaceId)
                    entityNames { id }
                }
            }"
        );
        AssertNull(lastTriggered);
    }

    /// <summary>Reports the value we got, so a default(Instant) is distinguishable from another row's value.</summary>
    private static void AssertNull(object? lastTriggered) => Assert.True(lastTriggered == null, $"expected null for the alert that never triggered, got {lastTriggered}");

    [Fact]
    public void BulkNullableIsNull_WithoutArgumentField()
    {
        var lastTriggered = LastTriggeredOfFirstAlert(
            @"query GetAlerts_Working1($spaceId: ID) {
                alerts(spaceId: $spaceId) { id lastTriggered entityNames { id } }
            }"
        );
        AssertNull(lastTriggered);
    }

    [Fact]
    public void BulkNullableIsNull_WithoutBulkListField()
    {
        var lastTriggered = LastTriggeredOfFirstAlert(
            @"query GetAlerts_Working2($spaceId: ID) {
                alerts(spaceId: $spaceId) { id lastTriggered lastTriggeredForEntity(entityId: $spaceId) }
            }"
        );
        AssertNull(lastTriggered);
    }

    [Fact]
    public void BulkNullableIsNull_OnASingleObject()
    {
        var lastTriggered = LastTriggeredOfFirstAlert(
            @"query GetAlert_Working3($spaceId: ID) {
                alert(id: ""c8f375bc-f14b-4f45-8da2-874342cf0ff8"") {
                    id lastTriggered lastTriggeredForEntity(entityId: $spaceId) entityNames { id }
                }
            }",
            "alert"
        );
        AssertNull(lastTriggered);
    }
}
