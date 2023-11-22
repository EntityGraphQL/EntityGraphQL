using System;
using System.Linq;
using EntityGraphQL;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks
{
    public abstract class BaseBenchmark
    {
        protected ServiceProvider Services { get; }
        protected SchemaProvider<BenchmarkContext> Schema { get; }

        public BaseBenchmark()
        {
            var servicesCollection = new ServiceCollection();
            ConfigureServices(servicesCollection);
            Services = servicesCollection.BuildServiceProvider();
            Schema = Services.GetRequiredService<SchemaProvider<BenchmarkContext>>();

            DataLoader.EnsureDbCreated(Services.GetRequiredService<BenchmarkContext>());
        }

        protected virtual SchemaProvider<BenchmarkContext> BuildSchema()
        {
            var schema = SchemaBuilder.FromObject<BenchmarkContext>();
            schema.UpdateType<Person>(type =>
            {
                type.AddField("name", person => $"{person.FirstName} {person.LastName}", "Person's full name");
            });
            return schema;
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services
                .AddDbContext<BenchmarkContext>()
                .AddSingleton(BuildSchema());
        }

        protected BenchmarkContext GetContext()
        {
            return Services.GetRequiredService<BenchmarkContext>();
        }

        /// <summary>
        /// Run query
        /// </summary>
        /// <param name="context"></param>
        /// <param name="query"></param>
        /// <param name="options"></param>
        /// <exception cref="InvalidOperationException"></exception>
        protected void RunQuery(BenchmarkContext context, QueryRequest query, ExecutionOptions? options = null)
        {
            var result = Schema.ExecuteRequestWithContext(query, context, Services, null, options);
            if (result.Errors != null)
                throw new InvalidOperationException("query failed: " + string.Join("\n", result.Errors.Select(m => m.Message)));
        }
    }
}