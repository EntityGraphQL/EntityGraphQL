using System;
using System.Collections.Generic;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Directives
{
    public interface IDirectiveProcessor
    {
        string Name { get; }
        string Description { get; }
        /// <summary>
        /// Return the Type used for the directive arguments
        /// </summary>
        /// <returns></returns>
        Type GetArgumentsType();
        /// <summary>
        /// Return true if the directive requires to make changes to the result
        /// </summary>
        /// <value></value>
        bool ProcessesResult { get; }
        BaseGraphQLField? ProcessField(BaseGraphQLField fieldResult, object arguments);
        IEnumerable<ArgType> GetArguments(ISchemaProvider schema, Func<string, string> fieldNamer);
    }
}