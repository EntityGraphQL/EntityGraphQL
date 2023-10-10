using Xunit;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace EntityGraphQL.Tests
{
    public class OptionsTests
    {
        [Fact]
        public void Test_ExecuteServiceFieldsSeparately_True_WithListNavigation()
        {
            var schema = SchemaBuilder.FromObject<OptionsContext>();
            var gql = new QueryRequest
            {
                Query = @"{
                    customers {
                        name
                        orders {
                            id    
                        }
                    }
                }"
            };
            var contextData = new OptionsContext().AddCustomerWithOrder("Lisa", 4);
            var res = schema.ExecuteRequestWithContext(gql, contextData, null, null);
            Assert.Null(res.Errors);
            dynamic customers = res.Data["customers"];
            Assert.Single(customers);
            Assert.Equal("Lisa", customers[0].name);
            Assert.Single(customers[0].orders);
            Assert.Equal(4, customers[0].orders[0].id);
        }
        [Fact]
        public void Test_ExecuteServiceFieldsSeparately_False_WithListNavigation()
        {
            var schema = SchemaBuilder.FromObject<OptionsContext>();
            schema.UpdateType<Customer>(type => type.ReplaceField("orders", null).ResolveWithService<AgeService>((customer, ageService) => customer.Orders).IsNullable(false));
            var gql = new QueryRequest
            {
                Query = @"{
                    customers {
                        name
                        orders {
                            id    
                        }
                    }
                }"
            };
            var contextData = new OptionsContext().AddCustomerWithOrder("Lisa", 4);

            var serviceCollection = new ServiceCollection();
            var ager = new AgeService();
            serviceCollection.AddSingleton(ager);
            var res = schema.ExecuteRequestWithContext(gql, contextData, serviceCollection.BuildServiceProvider(), null, new ExecutionOptions { ExecuteServiceFieldsSeparately = false });
            Assert.Null(res.Errors);
            dynamic customers = res.Data["customers"];
            Assert.Single(customers);
            Assert.Equal("Lisa", customers[0].name);
            Assert.Single(customers[0].orders);
            Assert.Equal(4, customers[0].orders[0].id);
        }
    }

    internal class OptionsContext
    {
        public IList<Customer> Customers { get; set; } = new List<Customer>();

        internal OptionsContext AddCustomerWithOrder(string customerName, int orderId)
        {
            var customer = new Customer
            {
                Name = customerName,
            };
            customer.Orders.Add(new Order { Id = orderId });
            Customers.Add(customer);
            return this;
        }
    }

    internal class Customer
    {
        public string Name { get; set; }
        public List<Order> Orders { get; set; } = new List<Order>();
    }

    internal class Order
    {
        public int Id { get; set; }
    }
}