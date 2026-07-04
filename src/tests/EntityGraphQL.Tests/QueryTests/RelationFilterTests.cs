using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests;

// Pins the patterns from issue #495 ("inner join"): filter parents to only those with a matching relation.
// many-to-one: client != null; one-to-many (paged): products.count() > 0 (enabled by the #378 fix)
public class RelationFilterTests
{
    private class Ctx
    {
        public List<Product> Products { get; set; } = [];
        public List<Client> Clients { get; set; } = [];
    }

    private class Product
    {
        public int Id { get; set; }
        public Client? Client { get; set; }
    }

    private class Client
    {
        public int Id { get; set; }
        public List<Product> Products { get; set; } = [];
    }

    [Fact]
    public void ManyToOneNullCheckAndOneToManyCountFilters()
    {
        var schema = SchemaBuilder.FromObject<Ctx>();
        schema.Query().GetField("products", null).UseFilter().UseOffsetPaging();
        schema.Query().GetField("clients", null).UseFilter().UseOffsetPaging();
        schema.Type<Client>().GetField("products", null).UseOffsetPaging();

        var ctx = new Ctx();
        var clientWithProducts = new Client { Id = 1 };
        var productWithClient = new Product { Id = 10, Client = clientWithProducts };
        clientWithProducts.Products.Add(productWithClient);
        ctx.Products.Add(productWithClient);
        ctx.Products.Add(new Product { Id = 11, Client = null }); // orphan
        ctx.Clients.Add(clientWithProducts);
        ctx.Clients.Add(new Client { Id = 2 }); // no products

        // "inner join" products -> client
        var r1 = schema.ExecuteRequestWithContext(new QueryRequest { Query = @"{ products(filter: ""client != null"") { items { id } } }" }, ctx, null, null);
        Assert.Null(r1.Errors);
        dynamic products = r1.Data!["products"]!;
        Assert.Equal(1, Enumerable.Count(products.items));
        Assert.Equal(10, Enumerable.First(products.items).id);

        // "inner join" clients -> products (paged child collection, #378 fix)
        var r2 = schema.ExecuteRequestWithContext(new QueryRequest { Query = @"{ clients(filter: ""products.count() > 0"") { items { id } } }" }, ctx, null, null);
        Assert.Null(r2.Errors);
        dynamic clients = r2.Data!["clients"]!;
        Assert.Equal(1, Enumerable.Count(clients.items));
        Assert.Equal(1, Enumerable.First(clients.items).id);
    }
}
