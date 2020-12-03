using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.EntityFramework.Extensions
{
    public class EntityGraphQLDbContextOptionsExtensionInfo : DbContextOptionsExtensionInfo
    {
        public EntityGraphQLDbContextOptionsExtensionInfo(IDbContextOptionsExtension extension) : base(extension)
        {
        }

        public override bool IsDatabaseProvider => false;

        public override string LogFragment => "EntityGraphQL:";

        public override long GetServiceProviderHashCode()
        {
            return LogFragment.GetHashCode() * 6;
        }

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
        }
    }

    public class EntityGraphQLDbContextOptionsExtension : IDbContextOptionsExtension
    {
        public DbContextOptionsExtensionInfo Info => new EntityGraphQLDbContextOptionsExtensionInfo(this);

        public void ApplyServices(IServiceCollection services)
        {
            services.AddSingleton<IMethodCallTranslatorPlugin, EntityGraphQLMethodCallTranslatorPlugin>();
        }

        public void Validate(IDbContextOptions options)
        {
        }
    }
}