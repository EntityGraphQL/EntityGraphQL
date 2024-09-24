using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests
{
    public class InheritanceAdvancedTest
    {
        public abstract class Entity
        {
            public int Id { get; set; }

            [GraphQLIgnore]
            public int TenantId { get; set; }
        }

        public class Order : Entity
        {
            public string Name { get; set; } = string.Empty;
            public Status? Status { get; set; }
            public ICollection<OrderItem> OrderItems { get; set; } = [];
        }

        public abstract class OrderItem : Entity
        {
            public Status? Status { get; set; }
        }

        public class BookOrderItem : OrderItem
        {
            public Book? Book { get; set; }
        }

        public class TShirtOrderItem : OrderItem
        {
            public int Size { get; set; }
            public int Colour { get; set; }
            public TShirt? TShirt { get; set; }

            public static string FormatTShirtPropertiesAsString(int size, int colour)
            {
                return $"{size} {colour}";
            }
        }

        public class Status
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public bool IsDeleted { get; set; }
        }

        public abstract class Product : Entity
        {
            public string Name { get; set; } = string.Empty;
        }

        public class Book : Product
        {
            public int Pages { get; set; }
            public string Author { get; set; } = string.Empty;
        }

        public class TShirt : Product { }

        public class TestContext
        {
            public IList<Order> Orders { get; set; } = [];
            public IList<Product> Products { get; set; } = [];
            public IList<Status> Statuses { get; set; } = [];
        }

        [Fact]
        public void BookStoreInheritanceTest()
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
                                TShirt = new TShirt() { Id = 3, Name = "SpiderMan" }
                            },
                            new BookOrderItem()
                            {
                                Status = new Status() { Id = 4, Name = "Shipped" },
                                Book = new Book()
                                {
                                    Author = "Ben Riley",
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
            // book and tshirt added with AddAllFields above
            schemaProvider.UpdateType<Book>(type => type.ImplementAllBaseTypes());
            schemaProvider.UpdateType<TShirt>(type => type.ImplementAllBaseTypes());

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
            var results = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
            Assert.False(results.HasErrors());
            var order = (dynamic)results.Data!["order"]!;

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

            Assert.Equal(4, order.orderItems[1].GetType().GetFields().Length);
            Assert.Equal(nameof(BookOrderItem), order.orderItems[1].__typename);
            Assert.Equal("Shipped", order.orderItems[1].status.name);

            Assert.Equal(2, order.orderItems[1].book.GetType().GetFields().Length);
            Assert.Null(order.orderItems[1].book.GetType().GetField("Author"));
            Assert.Equal("My Life", order.orderItems[1].book.name);
            Assert.Equal(300, order.orderItems[1].book.pages);
        }

        [Fact]
        public void BookStoreInheritanceTestWithFragments()
        {
            var context = new TestContext()
            {
                Orders =
                [
                    new Order()
                    {
                        Id = 1,
                        Name = "Barney",
                        Status = new Status() { Id = 0, Name = "Pending" },
                        OrderItems =
                        [
                            new TShirtOrderItem()
                            {
                                Colour = 1,
                                Size = 7,
                                Status = new Status() { Id = 2, Name = "BackOrder" },
                                TShirt = new TShirt() { Id = 3, Name = "SpiderMan" }
                            },
                            new BookOrderItem()
                            {
                                Status = new Status() { Id = 4, Name = "Shipped" },
                                Book = new Book()
                                {
                                    Author = "Ben Riley",
                                    Name = "My Life",
                                    Pages = 300
                                }
                            }
                        ]
                    }
                ]
            };

            var schemaProvider = SchemaBuilder.FromObject<TestContext>();
            schemaProvider.AddType<BookOrderItem>("BookOrderItem").ImplementAllBaseTypes().AddAllFields();
            schemaProvider.AddType<TShirtOrderItem>("TShirtOrderItem").ImplementAllBaseTypes().AddAllFields();
            // book and tshirt added with AddAllFields above
            schemaProvider.UpdateType<Book>(type => type.ImplementAllBaseTypes());
            schemaProvider.UpdateType<TShirt>(type => type.ImplementAllBaseTypes());

            // Simulate a JSON request with System.Text.Json
            var q = @"{
                ""query"": ""query Order($orderId: Int) {
                    order(id: $orderId) {
                        ...order
                    }
                }

                fragment order on Order {
                    id
                    name
                    status { id name }
                    orderItems {
                        ...orderItem
                    }
                }

                fragment orderItem on OrderItem {
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
                }"",
                ""variables"": {
                    ""orderId"": ""1""
                }
            }".Replace('\r', ' ').Replace('\n', ' ');
            var gql = JsonSerializer.Deserialize<QueryRequest>(q, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var results = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);
            Assert.False(results.HasErrors());
            var order = (dynamic)results.Data!["order"]!;

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

            Assert.Equal(4, order.orderItems[1].GetType().GetFields().Length);
            Assert.Equal(nameof(BookOrderItem), order.orderItems[1].__typename);
            Assert.Equal("Shipped", order.orderItems[1].status.name);

            Assert.Equal(2, order.orderItems[1].book.GetType().GetFields().Length);
            Assert.Null(order.orderItems[1].book.GetType().GetField("Author"));
            Assert.Equal("My Life", order.orderItems[1].book.name);
            Assert.Equal(300, order.orderItems[1].book.pages);
        }

        [Fact]
        public void InheritanceTestUsingMethod()
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
                                TShirt = new TShirt() { Id = 3, Name = "SpiderMan" }
                            },
                            new BookOrderItem()
                            {
                                Status = new Status() { Id = 4, Name = "Shipped" },
                                Book = new Book()
                                {
                                    Author = "Ben Riley",
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
            // book and tshirt added with AddAllFields above
            schemaProvider.UpdateType<Book>(type => type.ImplementAllBaseTypes());
            schemaProvider.UpdateType<TShirt>(type => type.ImplementAllBaseTypes());

            schemaProvider.UpdateType<TShirtOrderItem>(Order =>
            {
                Order.AddField("statusAsString", (o) => TShirtOrderItem.FormatTShirtPropertiesAsString(o.Size, o.Colour), "Get the order status as a string");
            });

            // Simulate a JSON request with System.Text.Json
            var q = @"{
                ""query"": ""query Order($orderId: Int) {
                    order(id: $orderId) {
                        ...order
                    }
                }

                fragment order on Order {
                    orderItems {
                        ...orderItem
                    }
                }

                fragment orderItem on OrderItem {
                    id
                    ... on TShirtOrderItem {                        
                        statusAsString
                    }
                }"",
                ""variables"": {
                    ""orderId"": ""1""
                }
            }".Replace('\r', ' ').Replace('\n', ' ');

            var gql = JsonSerializer.Deserialize<QueryRequest>(q, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var results = schemaProvider.ExecuteRequestWithContext(gql, context, null, null);

            if (results.HasErrors())
            {
                throw new Exception(results.Errors!.First().Message);
            }

            //Uncomment these guys to have the test fail properly
            Assert.False(results.HasErrors());
            var order = (dynamic)results.Data!["order"]!;
        }

        public class TestService
        {
            public string FormatTShirtPropertiesAsString(int colour, int size)
            {
                return $"colour: {colour} - size: {size}";
            }

            public string FormatTShirtPropertiesAsString(int colour, int size, int length)
            {
                return $"colour: {colour} - size: {size} - length: {length}";
            }
        }

        [Fact]
        public void InheritanceTestUsingResolveWithService()
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
                                TShirt = new TShirt() { Id = 3, Name = "SpiderMan" }
                            },
                            new BookOrderItem()
                            {
                                Status = new Status() { Id = 4, Name = "Shipped" },
                                Book = new Book()
                                {
                                    Author = "Ben Riley",
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
            // book and tshirt added with AddAllFields above
            schemaProvider.UpdateType<Book>(type => type.ImplementAllBaseTypes());
            schemaProvider.UpdateType<TShirt>(type => type.ImplementAllBaseTypes());

            schemaProvider.UpdateType<TShirtOrderItem>(Order =>
            {
                Order.AddField("statusAsString", "Get the order status as a string").ResolveWithService<TestService>((o, srv) => srv.FormatTShirtPropertiesAsString(o.Colour, o.Size));
            });

            // Simulate a JSON request with System.Text.Json
            var q = @"{
                ""query"": ""query Order($orderId: Int) {
                    order(id: $orderId) {
                        ...order
                    }
                }

                fragment order on Order {
                    orderItems {
                        ...orderItem
                    }
                }

                fragment orderItem on OrderItem {
                    id
                    ... on TShirtOrderItem {
                        statusAsString
                    }
                }"",
                ""variables"": {
                    ""orderId"": ""1""
                }
            }".Replace('\r', ' ').Replace('\n', ' ');

            var gql = JsonSerializer.Deserialize<QueryRequest>(q, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var testService = new TestService();
            var sc = new ServiceCollection();
            sc.AddSingleton(testService);
            var results = schemaProvider.ExecuteRequestWithContext(gql, context, sc.BuildServiceProvider(), null);

            if (results.HasErrors())
            {
                throw new Exception(results.Errors!.First().Message);
            }

            //Uncomment these guys to have the test fail properly
            Assert.False(results.HasErrors());
            var order = (dynamic)results.Data!["order"]!;

            Assert.Equal("colour: 1 - size: 7", order.orderItems[0].statusAsString);
        }

        [Fact]
        public void InheritanceTestUsingResolveWithServiceUsingArgs()
        {
            var context = new TestContext()
            {
                Orders =
                [
                    new Order()
                    {
                        Id = 1,
                        Name = "Barney",
                        Status = new Status() { Id = 0, Name = "Pending" },
                        OrderItems =
                        [
                            new TShirtOrderItem()
                            {
                                Colour = 1,
                                Size = 7,
                                Status = new Status() { Id = 2, Name = "BackOrder" },
                                TShirt = new TShirt() { Id = 3, Name = "SpiderMan" }
                            },
                            new BookOrderItem()
                            {
                                Status = new Status() { Id = 4, Name = "Shipped" },
                                Book = new Book()
                                {
                                    Author = "Ben Riley",
                                    Name = "My Life",
                                    Pages = 300
                                }
                            }
                        ]
                    }
                ]
            };

            var schemaProvider = SchemaBuilder.FromObject<TestContext>();
            schemaProvider.AddType<BookOrderItem>("BookOrderItem").ImplementAllBaseTypes().AddAllFields();
            schemaProvider.AddType<TShirtOrderItem>("TShirtOrderItem").ImplementAllBaseTypes().AddAllFields();
            // book and tshirt added with AddAllFields above
            schemaProvider.UpdateType<Book>(type => type.ImplementAllBaseTypes());
            schemaProvider.UpdateType<TShirt>(type => type.ImplementAllBaseTypes());

            schemaProvider.UpdateType<TShirtOrderItem>(Order =>
            {
                Order
                    .AddField("statusAsString", new { Length = 0 }, "Get the order status as a string")
                    .ResolveWithService<TestService>((o, args, srv) => srv.FormatTShirtPropertiesAsString(o.Colour, o.Size, args.Length));
            });

            // Simulate a JSON request with System.Text.Json
            var q = @"{
                ""query"": ""query Order($orderId: Int) {
                    order(id: $orderId) {
                        ...order
                    }
                }

                fragment order on Order {
                    orderItems {
                        ...orderItem
                    }
                }

                fragment orderItem on OrderItem {
                    id
                    ... on TShirtOrderItem {
                        statusAsString(length: 2)
                    }
                }"",
                ""variables"": {
                    ""orderId"": ""1""
                }
            }".Replace('\r', ' ').Replace('\n', ' ');

            var gql = JsonSerializer.Deserialize<QueryRequest>(q, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var sc = new ServiceCollection();
            sc.AddSingleton(new TestService());
            var results = schemaProvider.ExecuteRequestWithContext(gql, context, sc.BuildServiceProvider(), null);

            if (results.HasErrors())
            {
                throw new Exception(results.Errors!.First().Message);
            }

            //Uncomment these guys to have the test fail properly
            Assert.False(results.HasErrors());
            var order = (dynamic)results.Data!["order"]!;

            Assert.Equal("colour: 1 - size: 7 - length: 2", order.orderItems[0].statusAsString);
        }
    }
}
