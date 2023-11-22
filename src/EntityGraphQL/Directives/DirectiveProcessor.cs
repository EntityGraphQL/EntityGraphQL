using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Directives
{

    /// <summary>
    /// Base directive processor. To implement custom directives inherit from this class and override either or both
    /// ProcessQuery() - used to make changes to the query before execution (e.g. @include/skip)
    /// ProcessResult() - used to make changes to the result of the item the directive is on
    /// </summary>
    public abstract class DirectiveProcessor<TArguments> : IDirectiveProcessor
    {
        public Type GetArgumentsType() => typeof(TArguments);
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract List<ExecutableDirectiveLocation> Location { get; }

        private IDictionary<string, ArgType>? arguments;

        public virtual IGraphQLNode? VisitNode(ExecutableDirectiveLocation location, IGraphQLNode? node, object? arguments)
        {
            return node;
        }

        public IDictionary<string, ArgType> GetArguments(ISchemaProvider schema)
        {
            arguments ??= typeof(TArguments).GetProperties().ToList().Select(prop => ArgType.FromProperty(schema, prop, null)).ToDictionary(i => i.Name, i => i);
            return arguments;
        }
    }
}