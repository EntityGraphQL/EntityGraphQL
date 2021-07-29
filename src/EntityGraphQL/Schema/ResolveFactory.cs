using System;
using System.Linq.Expressions;

namespace EntityGraphQL.Schema
{
    public class ResolveFactory<TContext, TArguments, TSelection>
    {
        public Expression<Func<TContext, TArguments, TSelection>> PreSelection { get; set; }
        public Expression<Func<TSelection, TArguments, TSelection>> PostSelection { get; set; }
    }
}