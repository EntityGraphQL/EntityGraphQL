using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using EntityGraphQL.AspNet;
using System.Text.Json.Serialization;
using System.Text.Json;
using EntityGraphQL.Schema;

namespace demo
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env)
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
            services.AddDbContext<DemoContext>(opt => opt.UseSqlite("Filename=demo.db"));

            services.AddSingleton<AgeService>();

            services.AddLogging(logging =>
            {
                logging.AddConsole(configure => Configuration.GetSection("Logging"));
                logging.AddDebug();
            });
            // add schema provider so we don't need to create it everytime
            // if you want to override json serialization - say PascalCase response
            // You will also need to override the default fieldNamer in SchemaProvider
            // var jsonOptions = new JsonSerializerOptions
            // {
            //     // the generated types use fields
            //     IncludeFields = true,
            // };
            // services.AddSingleton<IGraphQLRequestDeserializer>(new DefaultGraphQLRequestDeserializer(jsonOptions));
            // services.AddSingleton<IGraphQLResponseSerializer>(new DefaultGraphQLResponseSerializer(jsonOptions));
            // Or you could overrise the whole inferface and do something other than JSON

            services.AddGraphQLSchema<DemoContext>(options =>
            {
                options.PreBuildSchemaFromContext = schema =>
                {
                    // add in needed mappings for our context
                    schema.AddScalarType<KeyValuePair<string, string>>("StringKeyValuePair", "Represents a pair of strings");
                };
                options.ConfigureSchema = GraphQLSchema.ConfigureSchema;
                // below this will generate the field names as they are from the reflected dotnet types - i.e matching the case
                // builder.FieldNamer = name => name;
            });

            services.AddRouting();
            services.AddControllers()
                .AddJsonOptions(opts =>
                {
                    // configure JSON serializer like this if you are return GraphQL execution results in your own controller
                    // assuming you want the default behavior of serializing GraphQL execution results to JSON
                    opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    opts.JsonSerializerOptions.IncludeFields = true;
                    opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, DemoContext db)
        {
            CreateData(db);

            app.UseFileServer();

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapGraphQL<DemoContext>(options: new ExecutionOptions
                {
#if DEBUG
                    IncludeDebugInfo = true
#endif
                });
            });
        }

        private static void CreateData(DemoContext db)
        {
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            // add test data
            var shawshank = new Movie
            {
                Name = "The Shawshank Redemption",
                Genre = Genre.Drama,
                Released = new DateTime(1994, 10, 14),
                Rating = 9.2,
                Director = new Person
                {
                    FirstName = "Frank",
                    LastName = "Darabont",
                    Dob = new DateTime(1959, 1, 28),
                },
                Actors = new List<Actor> {
                new Actor {
                    Person = new Person {
                        Dob = new DateTime(1958, 10, 16),
                        FirstName = "Tim",
                        LastName = "Robbins",
                    },
                },
            }
            };
            db.Movies.Add(shawshank);
            var francis = new Person
            {
                Dob = new DateTime(1939, 4, 7),
                FirstName = "Francis",
                LastName = "Coppola",
            };
            var godfather = new Movie
            {
                Name = "The Godfather",
                Genre = Genre.Drama,
                Released = new DateTime(1972, 3, 24),
                Rating = 9.2,
                Director = francis,
            };
            godfather.Actors = new List<Actor> {
                new Actor {
                    Person = new Person {
                        Dob = new DateTime(1924, 4, 3),
                        Died = new DateTime(2004, 7, 1),
                        FirstName = "Marlon",
                        LastName = "Brando",
                    },
                },
                new Actor {
                    Person = new Person {
                        Dob = new DateTime(1940, 4, 25),
                        FirstName = "Al",
                        LastName = "Pacino",
                    },
                },
            };
            godfather.Writers = new List<Writer> {
                new Writer {
                    Person = francis,
                }
            };

            db.Movies.Add(godfather);

            db.SaveChanges();
        }
    }
}
