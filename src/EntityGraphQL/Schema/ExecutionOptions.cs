using EntityGraphQL.LinqQuery;

namespace EntityGraphQL.Schema
{
    public class ExecutionOptions
    {
        public IMethodProvider MethodProvider { get; set; } = null;
        /// <summary>
        /// Turn on or off the pre selection of fields with no services.
        /// </summary>
        /// <value></value>
        public bool ExecuteServiceFieldsSeparately { get; set; } = true;
        /// <summary>
        /// Include debug timing information
        /// </summary>
        /// <value></value>
        public bool IncludeDebugInfo { get; set; } = false;
        /// <summary>
        /// Do not eecute the expression. Used for performance testing on EntityGraphQL code
        /// </summary>
        /// <value></value>
        public bool NoExecution { get; set; } = false;
    }
}