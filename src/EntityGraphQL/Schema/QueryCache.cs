using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Text;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Schema
{
    internal class QueryCache
    {
        private readonly MemoryCache cache;
        public QueryCache()
        {
            cache = new MemoryCache("EntityGraphQL.QueryCache");
        }

        internal (GraphQLDocument?, string) GetCompiledQuery(string query)
        {
            var hash = ComputeHash(query);
            var cached = (GraphQLDocument?)cache.Get(hash);
            return (cached, hash);
        }

        internal void AddCompiledQuery(string hash, GraphQLDocument compiledQuery)
        {
            cache.Add(hash, compiledQuery, new CacheItemPolicy { SlidingExpiration = new System.TimeSpan(0, 10, 0) });
        }

        internal string ComputeHash(string data)
        {
            using SHA256 sha256Hash = SHA256.Create();
            // ComputeHash - returns byte array  
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Encoding.UTF8.GetString(bytes);
        }
    }
}