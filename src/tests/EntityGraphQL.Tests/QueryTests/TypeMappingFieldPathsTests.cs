using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// AddTypeMapping describes the whole GraphQL type of a dotnet type - list-ness and nullability included, as
/// those come from the mapping string and not from the dotnet type. Every way of adding a field returning that
/// dotnet type has to use the mapping, not just the auto-populated ones. Only the auto path did: the others
/// resolved the type name (Point) but rebuilt the wrapper from the dotnet type, rendering `Point` where the
/// mapping says `[Point!]!`.
/// </summary>
public class TypeMappingFieldPathsTests
{
    private static SchemaProvider<ZoneContext> BuildSchema() =>
        SchemaBuilder.FromObject<ZoneContext>(
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

    /// <summary>The path that always worked - guards against losing it while fixing the others.</summary>
    [Fact]
    public void TestAutoPopulatedProperty()
    {
        Assert.Contains("shape: [Point!]!", BuildSchema().ToGraphQLSchemaString());
    }

    /// <summary>A regression in 6.0 - ProcessMethodIntoField stopped consulting the mapping.</summary>
    [Fact]
    public void TestGraphQLFieldMethod()
    {
        Assert.Contains("shapeFromMethod: [Point!]!", BuildSchema().ToGraphQLSchemaString());
    }

    /// <summary>
    /// Never worked: the mapping was looked up with the method's declared return type, so Task&lt;Polygon&gt;
    /// never matched. The lookup now happens on the awaited type.
    /// </summary>
    [Fact]
    public void TestAsyncGraphQLFieldMethod()
    {
        Assert.Contains("shapeFromAsyncMethod: [Point!]!", BuildSchema().ToGraphQLSchemaString());
    }

    [Fact]
    public void TestAddFieldWithExpression()
    {
        var schema = BuildSchema();
        schema.Type<Zone>().AddField("shapeAdded", z => z.Shape, "Added with an expression");
        Assert.Contains("shapeAdded: [Point!]!", schema.ToGraphQLSchemaString());
    }

    [Fact]
    public void TestAddFieldWithResolve()
    {
        var schema = BuildSchema();
        schema.Type<Zone>().AddField("shapeResolved", "Resolved from a service").Resolve<ShapeService>((z, srv) => srv.GetShape(z.Id));
        Assert.Contains("shapeResolved: [Point!]!", schema.ToGraphQLSchemaString());
    }

    [Fact]
    public void TestAddFieldOnQueryType()
    {
        var schema = BuildSchema();
        schema.Query().AddField("firstShape", ctx => ctx.Zones.First().Shape, "Root field");
        Assert.Contains("firstShape: [Point!]!", schema.ToGraphQLSchemaString());
    }

    [Fact]
    public void TestExpressionFactoryFieldFromAddFieldsFrom()
    {
        var schema = BuildSchema();
        schema.Type<Zone>().AddFieldsFrom<ZoneExtraFields>();
        Assert.Contains("shapeFromExpression: [Point!]!", schema.ToGraphQLSchemaString());
    }

    /// <summary>
    /// Taking the mapping's own GqlTypeInfo must not change how the field resolves - only how it is described.
    /// </summary>
    [Fact]
    public void TestAddedFieldWithMappedTypeStillResolves()
    {
        var schema = BuildSchema();
        schema.Type<Zone>().AddField("shapeAdded", z => z.Shape, "Added with an expression");

        var context = new ZoneContext { Zones = [new Zone { Id = 1, Shape = new Polygon([new Point { X = 1, Y = 2 }, new Point { X = 3, Y = 4 }]) }] };
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = "{ zones { shape shapeAdded } }" }, context, null, null);

        Assert.Null(result.Errors);
        dynamic zones = result.Data!["zones"]!;
        Assert.Equal(new Polygon([new Point { X = 1, Y = 2 }, new Point { X = 3, Y = 4 }]).Count(), ((IEnumerable<Point>)zones[0].shapeAdded).Count());
        Assert.Equal(1.0, ((IEnumerable<Point>)zones[0].shapeAdded).First().X);
    }

    /// <summary>
    /// What the wrong type actually costs a caller: introspection (and so every client codegen) described the
    /// field as a single nullable Point rather than a non-null list of non-null Points.
    /// </summary>
    [Fact]
    public void TestAddedFieldIsIntrospectedAsAList()
    {
        var schema = BuildSchema();
        schema.Type<Zone>().AddField("shapeAdded", z => z.Shape, "Added with an expression");

        var returnType = schema.Type<Zone>().GetField("shapeAdded", null).ReturnType;
        Assert.True(returnType.IsList);
        Assert.True(returnType.TypeNotNullable);
        Assert.False(returnType.ElementTypeNullable);
        Assert.Equal("Point", returnType.SchemaType.Name);
    }
}

public class ZoneContext
{
    public IEnumerable<Zone> Zones { get; set; } = [];
}

public class Zone
{
    public int Id { get; set; }
    public Polygon Shape { get; set; }

    [GraphQLField]
    public Polygon ShapeFromMethod() => Shape;

    [GraphQLField]
    public System.Threading.Tasks.Task<Polygon> ShapeFromAsyncMethod() => System.Threading.Tasks.Task.FromResult(Shape);
}

public class ZoneExtraFields : IFieldsFor<Zone>
{
    [GraphQLField("shapeFromExpression", "Shape via an expression factory")]
    public static Expression<Func<Zone, Polygon>> ShapeFromExpression() => zone => zone.Shape;
}

public class ShapeService
{
    public Polygon GetShape(int zoneId) => new([new Point { X = 0, Y = 0 }]);
}
