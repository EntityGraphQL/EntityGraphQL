using System;
using System.Linq;
using EntityGraphQL;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks
{
    public class BaseBenchmark
    {
        public ServiceProvider Services { get; }
        public SchemaProvider<BenchmarkContext> Schema { get; }

        public BaseBenchmark()
        {
            var servicesCollection = new ServiceCollection();
            ConfigureServices(servicesCollection);
            Services = servicesCollection.BuildServiceProvider();
            Schema = Services.GetService<SchemaProvider<BenchmarkContext>>();

            DataLoader.EnsureDbCreated(Services.GetService<BenchmarkContext>());
        }

        protected virtual SchemaProvider<BenchmarkContext> BuildSchema()
        {
            var schema = SchemaBuilder.FromObject<BenchmarkContext>();
            schema.UpdateType<Person>(type =>
            {
                type.AddField("name", person => $"{person.FirstName} {person.LastName}", "Person's full name");
            });
            schema.ReplaceField(
                "movies",
                new
                {
                    take = (int?)null
                },
                (ctx, args) => ctx.Movies.Take(args.take),
                "List of movies");
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
            return Services.GetService<BenchmarkContext>();
        }

        protected void RunQuery(BenchmarkContext context, QueryRequest query)
        {
            var result = Schema.ExecuteQuery(query, context, Services, null, new ExecutionOptions
            {
                NoExecution = true
            });
            if (result.Errors != null)
                throw new InvalidOperationException("query failed: " + string.Join("\n", result.Errors.Select(m => m.Message)));
        }
    }
}