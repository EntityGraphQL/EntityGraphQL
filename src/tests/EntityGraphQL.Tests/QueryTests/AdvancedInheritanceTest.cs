using EntityGraphQL.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace EntityGraphQL.Tests
{
    public abstract class Entity
    {
        public int Id { get; set; }
        [GraphQLIgnore]
        public int TenantId { get; set; }
    }

    public class Order : Entity
    {
        public string Name { get; set; }
        public Status Status { get; set; }
        public ICollection<OrderItem> OrderItems { get; set; }
    }

    public abstract class OrderItem : Entity
    {
        public Status Status { get; set; }
    }

    public class BookOrderItem : OrderItem
    {
        public Book Book { get; set; }
    }

    public class TShirtOrderItem : OrderItem
    {
        public int Size { get; set; }
        public int Colour { get; set; }
        public TShirt TShirt { get; set; }
    }

    public class Status
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsDeleted { get; set; }
    }

    public abstract class Product : Entity
    {
        public string Name { get; set; }
    }

    public class Book : Product
    {
        public int Pages { get; set; }
        public string Author { get; set; }
    }

    public class TShirt : Product
    {

    }

    public class TestContext
    {
        public IList<Order> Orders { get; set; }
        public IList<Product> Products { get; set; }
        public IList<Status> Statuses { get; set; }
    }

    public class AdvancedInheritanceTest
    {      
        [Fact]
        public void ITest()
        {
            var context = new TestContext()
            {
                Orders = new List<Order>()
                {
                    new Order()
                    {
                         Id = 1,
                         Name = "Barney",
                         Status = new Status() { Id = 0, Name = "Pending" },
                         OrderItems = new List<OrderItem>()
                         {
                              new TShirtOrderItem()
                              {
                                   Colour = 1,
                                   Size = 7,
                                   Status = new Status() { Id = 2, Name = "BackOrder" },
                                   TShirt = new TShirt()
                                    {
                                        Id = 3,
                                        Name = "SpiderMan"
                                    }
                              },
                              new BookOrderItem()
                              {
                                  Status = new Status() { Id = 4, Name = "Shipped "},
                                   Book = new Book()
                                   {
                                        Author = "Steve Riley",
                                         Name = "My Life",
                                          Pages = 300
                                   }
                              }
                         }
                    }
                }
            };

            var schemaProvider = SchemaBuilder.FromObject<TestContext>();
            schemaProvider.AddType<BookOrderItem>("BookOrderItem").ImplementAllBaseTypes().AddAllFields();
            schemaProvider.AddType<TShirtOrderItem>("TShirtOrderItem").ImplementAllBaseTypes().AddAllFields();
            schemaProvider.AddType<Book>("Book").ImplementAllBaseTypes().AddAllFields();
            schemaProvider.AddType<TShirt>("TShirt").ImplementAllBaseTypes().AddAllFields();

            // Simulate a JSON request with System.Text.Json
            var q = @"{
""query"": ""query Order($orderId: Int) {
        order(id: $orderId) {
            id
            name
            status { id name }
            orderItems {
                __typename
                id
                status  { id name }
                ... on BookOrderItem {
                    book {
                        name
                        pages
                    }                    
                }
                ... on TShirtOrderItem {
                    size
                    colour
                    tShirt {
                        name
                    }
                }
            }
        }
    }"",
                ""variables"": {
                    ""orderId"": ""1""
                }
            }".Replace('\r', ' ').Replace('\n', ' ');
            var gql = System.Text.Json.JsonSerializer.Deserialize<QueryRequest>(q, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var results = schemaProvider.ExecuteRequest(gql, context, null, null);
            Assert.False(results.HasErrors());
            var order = (dynamic)results.Data["order"];

            Assert.Equal(4, order.GetType().GetFields().Length);
            Assert.Equal(1, order.id);
            Assert.Equal("Barney", order.name);
            Assert.Equal("Pending", order.status.name);
            Assert.Equal(2, order.orderItems.Count);

            Assert.Equal(6, order.orderItems[0].GetType().GetFields().Length);
            Assert.Equal(nameof(TShirtOrderItem), order.orderItems[0].__typename);
            Assert.Equal(1, order.orderItems[0].colour);
            Assert.Equal(7, order.orderItems[0].size);
            Assert.Equal("BackOrder", order.orderItems[0].status.name);            
            
            Assert.Equal(1, order.orderItems[0].tShirt.GetType().GetFields().Length);
            Assert.Equal("SpiderMan", order.orderItems[0].tShirt.name);
            Assert.Null(order.orderItems[0].tShirt.GetType().GetField("Tenant"));

            Assert.Equal(6, order.orderItems[1].GetType().GetFields().Length);
            Assert.Equal(nameof(BookOrderItem), order.orderItems[1].__typename);
            Assert.Equal("Shipped", order.orderItems[1].status.name);

            Assert.Equal(2, order.orderItems[1].book.GetType().GetFields().Length);
            Assert.Equal("Steve Riley", order.orderItems[1].book.author);
            Assert.Equal("My Life", order.orderItems[1].book.name);
            Assert.NotEqual(300, order.orderItems[1].book.pages);
        }
    }
}