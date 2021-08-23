using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Schema
{
    public class QueryCache
    {
        private readonly Dictionary<string, GraphQLDocument> cache = new();

        public (GraphQLDocument queryResult, string hash) Get(QueryRequest gql)
        {
            var hash = ComputeHash(gql);
            if (cache.ContainsKey(hash))
                return (cache[hash], hash);
            return (null, hash);
        }

        public void Put(string hash, GraphQLDocument queryResult)
        {
            lock (cache)
            {
                cache[hash] = queryResult;
            }
        }
        private string ComputeHash(QueryRequest gql)
        {
            var hasher = SHA256.Create();
            var hash = Encoding.UTF8.GetString(hasher.ComputeHash(Encoding.UTF8.GetBytes(gql.Query)));
            return hash;
        }
    }
}