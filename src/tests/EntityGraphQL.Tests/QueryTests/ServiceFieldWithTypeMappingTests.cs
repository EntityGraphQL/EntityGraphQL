using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// Covers a service-resolved field on a parent type combined with a child collection field whose
/// element type uses AddTypeMapping (a CLR type mapped to a GraphQL list type). This combination
/// previously failed during the two-pass (service field) execution because the second pass tried to
/// infer the list element type from the mapped CLR type via GetEnumerableOrArrayType(), which returns
/// null for a non-generic type that implements IEnumerable&lt;T&gt; (e.g. NpgsqlPolygon).
///
/// Mirrors the real world case: Floor has a service field `latestSightingCount` (.Resolve&lt;IService&gt;)
/// and a child collection `spaces { shape }` where FloorSpace.Shape is a CLR type (NpgsqlPolygon)
/// mapped to "[Point!]!" via AddTypeMapping.
/// </summary>
public class ServiceFieldWithTypeMappingTests
{
    [Fact]
    public void TestServiceFieldOnParentWithTypeMappedChildCollectionField()
    {
        // Register the Point type and the Polygon -> [Point!]! mapping before the schema is built
        // from the context so the FloorSpace.Shape field is created with the mapped GraphQL type.
        var schema = SchemaBuilder.FromObject<BuildingContext>(
            new SchemaProviderOptions(),
            new SchemaBuilderOptions
            {
                PreBuildSchemaFromContext = s =>
                {
                    s.AddScalarType<Point>("Point", "A 2D point");
                    s.AddTypeMapping<Polygon>("[Point!]!");
                },
            }
        );

        // Service-resolved field on the parent type (Floor)
        schema.Type<Floor>().AddField("latestSightingCount", "Number of latest sightings").Resolve<SightingService>((floor, srv) => srv.GetLatestSightingCount(floor.Id));

        var context = new BuildingContext
        {
            Floors =
            [
                new Floor
                {
                    Id = 1,
                    Name = "Ground Floor",
                    Spaces =
                    [
                        new FloorSpace
                        {
                            Id = 10,
                            Name = "Lobby",
                            Shape = new Polygon([new Point { X = 0, Y = 0 }, new Point { X = 1, Y = 0 }, new Point { X = 1, Y = 1 }]),
                        },
                    ],
                },
            ],
        };

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(new SightingService());
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Requesting the service field AND the type-mapped child collection field together
        var gql = new QueryRequest
        {
            Query =
                @"{
                    floors {
                        latestSightingCount
                        spaces {
                            shape
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, context, serviceProvider, null);

        // Combining the service field and the type-mapped child collection field used to fail with
        // "Value cannot be null. (Parameter 'type')" during the two-pass (service field) execution.
        Assert.Null(result.Errors);
        Assert.NotNull(result.Data);

        dynamic floors = result.Data!["floors"]!;
        Assert.Single((IEnumerable<dynamic>)floors);
        Assert.Equal(42, (int)floors[0].latestSightingCount);
        dynamic spaces = floors[0].spaces;
        Assert.Single((IEnumerable<dynamic>)spaces);
        dynamic shape = spaces[0].shape;
        Assert.Equal(3, Enumerable.Count((IEnumerable<dynamic>)shape));
    }

    [Fact]
    public void TestTypeMappedChildCollectionFieldAloneWorks()
    {
        var schema = SchemaBuilder.FromObject<BuildingContext>(
            new SchemaProviderOptions(),
            new SchemaBuilderOptions
            {
                PreBuildSchemaFromContext = s =>
                {
                    s.AddScalarType<Point>("Point", "A 2D point");
                    s.AddTypeMapping<Polygon>("[Point!]!");
                },
            }
        );

        var context = new BuildingContext
        {
            Floors =
            [
                new Floor
                {
                    Id = 1,
                    Name = "Ground Floor",
                    Spaces =
                    [
                        new FloorSpace
                        {
                            Id = 10,
                            Name = "Lobby",
                            Shape = new Polygon([new Point { X = 0, Y = 0 }]),
                        },
                    ],
                },
            ],
        };

        var gql = new QueryRequest { Query = @"{ floors { id spaces { id shape } } }" };

        var result = schema.ExecuteRequestWithContext(gql, context, null, null);
        Assert.Null(result.Errors);

        dynamic floors = result.Data!["floors"]!;
        dynamic shape = floors[0].spaces[0].shape;
        // shape is mapped to [Point!]! so it must be a list
        Assert.Single((IEnumerable<dynamic>)shape);
    }

    [Fact]
    public void TestServiceFieldOnParentAloneWorks()
    {
        var schema = SchemaBuilder.FromObject<BuildingContext>(
            new SchemaProviderOptions(),
            new SchemaBuilderOptions
            {
                PreBuildSchemaFromContext = s =>
                {
                    s.AddScalarType<Point>("Point", "A 2D point");
                    s.AddTypeMapping<Polygon>("[Point!]!");
                },
            }
        );

        schema.Type<Floor>().AddField("latestSightingCount", "Number of latest sightings").Resolve<SightingService>((floor, srv) => srv.GetLatestSightingCount(floor.Id));

        var context = new BuildingContext
        {
            Floors =
            [
                new Floor
                {
                    Id = 1,
                    Name = "Ground Floor",
                    Spaces = [],
                },
            ],
        };

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(new SightingService());

        var gql = new QueryRequest { Query = @"{ floors { id name latestSightingCount } }" };

        var result = schema.ExecuteRequestWithContext(gql, context, serviceCollection.BuildServiceProvider(), null);
        Assert.Null(result.Errors);
    }
}

public class BuildingContext
{
    public List<Floor> Floors { get; set; } = [];
}

public class Floor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IEnumerable<FloorSpace> Spaces { get; set; } = [];
}

public class FloorSpace
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Polygon Shape { get; set; }
}

/// <summary>
/// Mimics NpgsqlPolygon - a CLR type that is enumerable of points and mapped to "[Point!]!".
/// </summary>
public readonly struct Polygon(IReadOnlyList<Point> points) : IEnumerable<Point>
{
    private readonly IReadOnlyList<Point> points = points;

    public IEnumerator<Point> GetEnumerator() => (points ?? []).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class Point
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class SightingService
{
    public int GetLatestSightingCount(int floorId) => 42;
}
