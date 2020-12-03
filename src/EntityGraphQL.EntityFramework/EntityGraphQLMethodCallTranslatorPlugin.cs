using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityGraphQL.EntityFramework.Extensions
{
    /// <summary>
    /// This is where we can collect all our method translators
    /// </summary>
    public class EntityGraphQLMethodCallTranslatorPlugin : IMethodCallTranslatorPlugin
    {
        public IEnumerable<IMethodCallTranslator> Translators { get; }

        public EntityGraphQLMethodCallTranslatorPlugin()
        {
            Translators = new List<IMethodCallTranslator>
                    {
                        new WhereWhenMethodCallTranslator()
                    };
        }
    }
}