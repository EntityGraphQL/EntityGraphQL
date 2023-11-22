using System;
using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Text;
using EntityGraphQL.Compiler;

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
            hash ??= ComputeHash(query);
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

        public static string ComputeHash(string data)
        {
            using SHA256 sha256Hash = SHA256.Create();
            // ComputeHash - returns byte array  
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(data));
            return ByteToHexBitFiddle(bytes);
        }

        // thanks https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa/14333437#14333437
        private static string ByteToHexBitFiddle(byte[] bytes)
        {
            char[] c = new char[bytes.Length * 2];
            int b;
            for (int i = 0; i < bytes.Length; i++)
            {
                b = bytes[i] >> 4;
                c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
                b = bytes[i] & 0xF;
                c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
            }
            return new string(c);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            cache.Dispose();
        }
    }
}