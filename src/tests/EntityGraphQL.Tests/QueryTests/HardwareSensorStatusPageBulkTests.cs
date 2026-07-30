using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// Exact repro of XyAdmin Status → Offline Active:
/// <c>hardwareSensorStatusPage { items { lastSeen isOffline floor { currentFloorStatus } } }</c>
/// where the page is a root <c>Resolve</c> returning <see cref="OffsetPage{T}"/> of inventory rows,
/// and inventory bulk keys use a nullable navigation (<c>DetexyBoard == null ? null : DetexyBoard.SerialNumber</c>).
/// </summary>
public class HardwareSensorStatusPageBulkTests
{
    public enum HardwareSensorType
    {
        Area = 0,
        Entry = 1,
        Presence = 2,
    }

    public class DetexyBoard
    {
        public string SerialNumber { get; set; } = string.Empty;
    }

    public class FloorStatus
    {
        public string Type { get; set; } = string.Empty;
        public DateTime? Started { get; set; }
    }

    public class Floor
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class HardwareSensorInventory
    {
        public Guid Id { get; set; }
        public Guid SensorId { get; set; }
        public HardwareSensorType SensorType { get; set; }
        public string SerialOrExternalId { get; set; } = string.Empty;
        public DetexyBoard? DetexyBoard { get; set; }
        public Floor? Floor { get; set; }
    }

    /// <summary>Mirrors CoreData <c>HardwareSensorStatusPage : OffsetPage&lt;HardwareSensorInventory&gt;</c>.</summary>
    public class HardwareSensorStatusPage : OffsetPage<HardwareSensorInventory>
    {
        public HardwareSensorStatusPage(int totalItems, int skip, int take)
            : base(skip, take)
        {
            TotalItems = totalItems;
        }
    }

    public class EmptyContext { }

    /// <summary>Mirrors <c>HardwareSensorLiveStatusBulk.Key(type, boardSerial, externalId, sensorId)</c>.</summary>
    public static class HardwareSensorLiveStatusBulkKeys
    {
        private const char Sep = '\u001f';

        public static string Key(HardwareSensorType sensorType, string? areaSerial, string serialOrExternalId, Guid sensorId) =>
            string.Join(Sep, ((int)sensorType).ToString(), areaSerial ?? string.Empty, serialOrExternalId ?? string.Empty, sensorId.ToString("D"));
    }

    public class HardwareSensorLiveStatusBulk
    {
        public int LastSeenCallCount { get; private set; }
        public int IsOfflineCallCount { get; private set; }
        public Dictionary<string, DateTime?> LastSeenByKey { get; } = [];
        public Dictionary<string, bool> OfflineByKey { get; } = [];

        public DateTime? LastSeenOne(string key) => LastSeenByKey.TryGetValue(key, out var v) ? v : null;

        public bool IsOfflineOne(string key) => OfflineByKey.TryGetValue(key, out var v) && v;

        public IDictionary<string, DateTime?> LastSeenMany(IEnumerable<string> keys)
        {
            LastSeenCallCount++;
            return keys.Distinct().ToDictionary(k => k, k => LastSeenByKey.TryGetValue(k, out var v) ? v : (DateTime?)null);
        }

        public IDictionary<string, bool> IsOfflineMany(IEnumerable<string> keys)
        {
            IsOfflineCallCount++;
            return keys.Distinct().ToDictionary(k => k, k => OfflineByKey.TryGetValue(k, out var v) && v);
        }
    }

    public class FloorStatusProvider
    {
        public int CallCount { get; private set; }
        public Dictionary<Guid, FloorStatus> Statuses { get; } = [];

        public FloorStatus? GetOne(Guid floorId) => Statuses.TryGetValue(floorId, out var s) ? s : null;

        public IDictionary<Guid, FloorStatus> GetCurrentFloorStatuses(IEnumerable<Guid> floorIds)
        {
            CallCount++;
            return floorIds.Distinct().Where(Statuses.ContainsKey).ToDictionary(id => id, id => Statuses[id]);
        }
    }

    public class HardwareSensorStatusPageService
    {
        public List<HardwareSensorInventory> Rows { get; set; } = [];

        public HardwareSensorStatusPage GetPage()
        {
            return new HardwareSensorStatusPage(Rows.Count, 0, 50) { Items = Rows };
        }
    }

    [Fact]
    public void HardwareSensorStatusPage_LastSeenAndIsOffline_NullableDetexyBoardBulkKey_Works()
    {
        var schema = SchemaBuilder.FromObject<EmptyContext>();
        schema.AddEnum<HardwareSensorType>("HardwareSensorType", "sensor type");
        schema.AddType<DetexyBoard>("DetexyBoard", "board").AddAllFields();
        schema.AddType<FloorStatus>("FloorStatus", "floor status").AddAllFields();
        schema.AddType<Floor>("Floor", "floor").AddAllFields();
        schema.AddType<HardwareSensorInventory>("HardwareSensorInventory", "inventory row").AddAllFields();
        schema.AddType<HardwareSensorStatusPage>("HardwareSensorStatusPage", "paged status").AddAllFields();

        // Nested ResolveBulk as in StatusSensorPage → floor.currentFloorStatus
        schema
            .Type<Floor>()
            .AddField("currentFloorStatus", "current status")
            .Resolve<FloorStatusProvider>((floor, fsp) => fsp.GetOne(floor.Id))
            .ResolveBulk<FloorStatusProvider, Guid, FloorStatus>(floor => floor.Id, (ids, fsp) => fsp.GetCurrentFloorStatuses(ids));

        // Exact Resolve + ResolveBulk shape from HardwareSensorGraphQLExtensions
        schema
            .Type<HardwareSensorInventory>()
            .AddField("lastSeen", "last seen")
            .Resolve<HardwareSensorLiveStatusBulk>(
                (row, bulk) =>
                    bulk.LastSeenOne(HardwareSensorLiveStatusBulkKeys.Key(row.SensorType, row.DetexyBoard == null ? null : row.DetexyBoard.SerialNumber, row.SerialOrExternalId, row.SensorId))
            )
            .ResolveBulk<HardwareSensorLiveStatusBulk, string, DateTime?>(
                row => HardwareSensorLiveStatusBulkKeys.Key(row.SensorType, row.DetexyBoard == null ? null : row.DetexyBoard.SerialNumber, row.SerialOrExternalId, row.SensorId),
                (keys, bulk) => bulk.LastSeenMany(keys)
            );

        schema
            .Type<HardwareSensorInventory>()
            .AddField("isOffline", "offline")
            .Resolve<HardwareSensorLiveStatusBulk>(
                (row, bulk) =>
                    bulk.IsOfflineOne(HardwareSensorLiveStatusBulkKeys.Key(row.SensorType, row.DetexyBoard == null ? null : row.DetexyBoard.SerialNumber, row.SerialOrExternalId, row.SensorId))
            )
            .ResolveBulk<HardwareSensorLiveStatusBulk, string, bool>(
                row => HardwareSensorLiveStatusBulkKeys.Key(row.SensorType, row.DetexyBoard == null ? null : row.DetexyBoard.SerialNumber, row.SerialOrExternalId, row.SensorId),
                (keys, bulk) => bulk.IsOfflineMany(keys)
            );

        schema.Query().AddField("hardwareSensorStatusPage", "Status page").Resolve<HardwareSensorStatusPageService>((_, svc) => svc.GetPage());

        var floorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var areaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var entryId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var floor = new Floor { Id = floorId, Name = "L1" };

        var areaKey = HardwareSensorLiveStatusBulkKeys.Key(HardwareSensorType.Area, "SN-AREA", "ext-a", areaId);
        var entryKey = HardwareSensorLiveStatusBulkKeys.Key(HardwareSensorType.Entry, null, "ext-e", entryId);

        var pageService = new HardwareSensorStatusPageService
        {
            Rows =
            [
                new HardwareSensorInventory
                {
                    Id = Guid.NewGuid(),
                    SensorId = areaId,
                    SensorType = HardwareSensorType.Area,
                    SerialOrExternalId = "ext-a",
                    DetexyBoard = new DetexyBoard { SerialNumber = "SN-AREA" },
                    Floor = floor,
                },
                new HardwareSensorInventory
                {
                    Id = Guid.NewGuid(),
                    SensorId = entryId,
                    SensorType = HardwareSensorType.Entry,
                    SerialOrExternalId = "ext-e",
                    DetexyBoard = null, // Entry path — nullable nav must not break bulk key rewrite
                    Floor = floor,
                },
            ],
        };

        var liveStatus = new HardwareSensorLiveStatusBulk
        {
            LastSeenByKey = { [areaKey] = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), [entryKey] = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc) },
            OfflineByKey = { [areaKey] = true, [entryKey] = false },
        };
        var floorStatuses = new FloorStatusProvider
        {
            Statuses =
            {
                [floorId] = new FloorStatus { Type = "Live", Started = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc) },
            },
        };

        var services = new ServiceCollection();
        services.AddSingleton(new EmptyContext());
        services.AddSingleton(pageService);
        services.AddSingleton(liveStatus);
        services.AddSingleton(floorStatuses);
        var sp = services.BuildServiceProvider();

        // Mirrors StatusViewQueries STATUS_SENSOR_PAGE_QUERY selection for the failing fields
        var res = schema.ExecuteRequest(
            new QueryRequest
            {
                Query =
                    @"{
                    hardwareSensorStatusPage {
                        totalItems
                        items {
                            sensorId
                            sensorType
                            serialOrExternalId
                            lastSeen
                            isOffline
                            floor {
                                id
                                name
                                currentFloorStatus {
                                    type
                                    started
                                }
                            }
                        }
                    }
                }",
            },
            sp,
            null
        );

        if (res.Errors != null)
            Assert.Fail(string.Join(" | ", res.Errors.Select(e => $"{e.Message} path=[{string.Join('.', e.Path ?? [])}]")));

        Assert.Equal(1, liveStatus.LastSeenCallCount);
        Assert.Equal(1, liveStatus.IsOfflineCallCount);
        Assert.Equal(1, floorStatuses.CallCount);

        dynamic page = res.Data!["hardwareSensorStatusPage"]!;
        Assert.Equal(2, page.totalItems);
        Assert.Equal(2, page.items.Count);
        Assert.True(page.items[0].isOffline);
        Assert.False(page.items[1].isOffline);
        Assert.Equal("Live", page.items[0].floor.currentFloorStatus.type);
        Assert.Equal("Live", page.items[1].floor.currentFloorStatus.type);
    }
}
