using System;
using System.Linq.Expressions;
using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Text;
using EntityGraphQL.Compiler;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema
{
    public class DelegateCache : IDisposable
    {
        private readonly MemoryCache cache;
        public DelegateCache()
        {
            cache = new MemoryCache("EntityGraphQL.DelegateCache");
        }

        public Delegate GetCompiledExpression(LambdaExpression expression)
        {
            var hash = expression.ToString().ComputeHash();
            var cached = (Delegate?)cache.Get(hash);

            if(cached ==  null)
            {
                cached = expression.Compile();
                cache.Add(hash, cached, new CacheItemPolicy { SlidingExpiration = new System.TimeSpan(0, 10, 0) });
            }

            return cached;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            cache.Dispose();
        }

    }
}