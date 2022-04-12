namespace EntityGraphQL.Schema
{
    public class ExecutionOptions
    {
        /// <summary>
        /// Turn on or off the pre selection of fields with no services.
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
        /// Include debug timing information
        /// </summary>
        /// <value></value>
        public bool IncludeDebugInfo { get; set; } = false;
#if DEBUG
        /// <summary>
        /// Do not eecute the expression. Used for performance testing on EntityGraphQL code
        /// </summary>
        /// <value></value>
        public bool NoExecution { get; set; } = false;
#endif
    }
}