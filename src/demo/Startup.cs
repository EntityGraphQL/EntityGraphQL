using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EntityQueryLanguage;
using EntityQueryLanguage.DataApi;
using Microsoft.EntityFrameworkCore;

namespace demo
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<DemoContext>(opt => opt.UseInMemoryDatabase());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            // add test data
            var context = app.ApplicationServices.GetService<DemoContext>();
            context.Locations.Add(new Location {Id = 11, Name = "My House"});
            context.Locations.Add(new Location {Id = 12, Name = "The White House"});
            context.SaveChanges();

            var demoSchema = new ObjectSchemaProvider<DemoContext>();
            // we can extend the schema
            demoSchema.ExtendType<Location>("dumb", l => l.Id + " - " + l.Name);
            demoSchema.Type<DemoContext>("Stats", "Some stats", new {
                TotalLocations = demoSchema.Field((DemoContext c) => c.Locations.Count(), "Some desciption")
            });

            app.UseEql("/api/query", demoSchema, () => app.ApplicationServices.GetService<DemoContext>());
        }
    }
}
