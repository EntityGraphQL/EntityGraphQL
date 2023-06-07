using System;
using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Text;
using EntityGraphQL.Compiler;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema
{
    public class QueryCache : IDisposable
    {
        private readonly MemoryCache cache;
        public QueryCache()
        {
            cache = new MemoryCache("EntityGraphQL.QueryCache");
        }

        public (GraphQLDocument?, string) GetCompiledQuery(string query, string? hash)
        {
            hash ??= query.ComputeHash();
            var cached = GetCompiledQueryWithHash(hash);
            return (cached, hash);
        }

        public GraphQLDocument? GetCompiledQueryWithHash(string hash)
        {
            var cached = (GraphQLDocument?)cache.Get(hash);
            return cached;
        }

        public void AddCompiledQuery(string hash, GraphQLDocument compiledQuery)
        {
            cache.Add(hash, compiledQuery, new CacheItemPolicy { SlidingExpiration = new System.TimeSpan(0, 10, 0) });
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            cache.Dispose();
        }
    }
}