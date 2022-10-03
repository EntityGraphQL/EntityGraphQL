using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using System.Text.Json;
using EntityGraphQL.Schema;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using EntityGraphQL.AspNet;
using demo.Infrastructure;

[assembly: FunctionsStartup(typeof(demo.Startup))]


namespace demo
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddDbContext<DemoContext>(opt => opt.UseSqlite("Filename=demo.db"));

            builder.Services.AddSingleton<AgeService>();
            builder.Services.AddTransient<ClaimsPrincipalAccessor>();

            builder.Services.AddGraphQLSchema<DemoContext>(options =>
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
        }
    }
}
