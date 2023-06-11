using System;
using System.Linq.Expressions;

namespace EntityGraphQL.Schema
{
    public class ExecutionOptions
    {
        /// <summary>
        /// Turn on or off the pre selection of fields with no services.
        /// When enabled, EntityGraphQL will build an Expression that selects the whole object graph without service 
        /// dependant fields (including any core context fields a service field requires). Then once executed it will
        /// build and execute a second Expression tree that will include the service fields.
        /// 
        /// This allows the use of a ORM like EF where the first pass will all be translated into SQL by the ORM. Then in memory 
        /// the second execution can use that data to build the final result uses the dependant services.
        /// </summary>
        /// <value></value>
        public bool ExecuteServiceFieldsSeparately { get; set; } = true;
        /// <summary>
        /// Enable support for persisted queries - https://www.apollographql.com/docs/react/api/link/persisted-queries/
        /// This will enable the cache as well
        /// </summary>
        public bool EnablePersistedQueries { get; set; } = true;
        /// <summary>
        /// Enables a cache of recently compiled queries to speed up execution of highly used queries.
        /// Cache is used for persisted queries as well.
        /// </summary>
        public bool EnableQueryCache { get; set; } = true;

        /// <summary>
        /// Allows you to hook into just before an expression is executed and modify it to suit. Note that if 
        /// <code>ExecuteServiceFieldsSeparately</code> is true, this will be called twice if your query includes fields with serivces.
        /// Second parameter bool isFinal == true if the expression is the final execution - this means
        ///  - ExecuteServiceFieldsSeparately = false, or
        ///  - The query does not reference any fields with services
        ///  - The query references fields with service and the first execution has completed (isFinal == false) and we are executing again to merge the service results
        /// </summary>
        public Func<Expression, bool, Expression>? BeforeExecuting { get; set; }

#if DEBUG
        /// <summary>
        /// Include timing information about query execution
        /// </summary>
        /// <value></value>
        public bool IncludeDebugInfo { get; set; }

        /// <summary>
        /// Do not execute the expression. Used for performance testing on EntityGraphQL code
        /// </summary>
        /// <value></value>
        public bool NoExecution { get; set; }
#endif
    }
}