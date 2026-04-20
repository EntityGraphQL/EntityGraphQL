using System;
using EntityGraphQL.Schema.QueryLimits;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EntityGraphQL.AspNet;

/// <summary>
/// DI helpers for registering the default per-field rate limit service.
/// </summary>
public static class EntityGraphQLAspNetRateLimitExtensions
{
    /// <summary>
    /// Register the default in-memory <see cref="IFieldRateLimitService"/> backed by
    /// <see cref="System.Threading.RateLimiting.PartitionedRateLimiter{TResource}"/>. Tag fields with
    /// <c>field.AddRateLimit("policy-name")</c> — the policy name must match one configured here.
    ///
    /// Consumers wanting a distributed backend should register their own <see cref="IFieldRateLimitService"/>
    /// implementation instead of calling this method.
    /// </summary>
    public static IServiceCollection AddGraphQLFieldRateLimit(this IServiceCollection services, Action<GraphQLFieldRateLimitOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        services.Configure(configure);
        services.TryAddSingleton<IFieldRateLimitService, DefaultFieldRateLimitService>();
        return services;
    }
}
