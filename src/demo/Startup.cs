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
            services.AddDbContext<DemoContext>(opt => opt.UseInMemoryDatabase("demo-db"));

            // add schema provider so we don't need to create it everytime
            services.AddSingleton<MappedSchemaProvider<DemoContext>>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            // add test data
            var context = app.ApplicationServices.GetService<DemoContext>();
            context.Properties.Add(new Property {Id = 11, Name = "My House", Location = new Location {Id = 21, Name = "Australia"}});
            context.Properties.Add(new Property {Id = 12, Name = "The White House", Location = new Location {Id = 22, Name = "America", SomeInt = 9999}});
            context.SaveChanges();

            var demoSchema = SchemaBuilder.FromObject<DemoContext>();
            // we can extend the schema
            demoSchema.Type<Location>().AddField("dumb", l => l.Id + " - " + l.Name, "A dumb field");

            app.UseMvc();
        }
    }
}
