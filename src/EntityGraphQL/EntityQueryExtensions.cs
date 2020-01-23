using System;
using System.Diagnostics;
using EntityGraphQL.Schema;
using EntityGraphQL.Compiler;
using EntityGraphQL.LinqQuery;
using System.Security.Claims;

namespace EntityGraphQL
{
    /// <summary>
    /// Provides the extension methods that are the entry point for querying an object
    /// </summary>
    public static class EntityQueryExtensions
    {
        /// <summary>
        /// Extension method to query an object purely based on the a defined schema of that object.
        /// </summary>
        /// <param name="context">The root of your object graph you are querying. E.g. a DbContext</param>
        /// <param name="query">GraphQL query</param>
        /// <param name="schemaProvider">Schema definition. Defines new fields/entities. Maps names, etc.</param>
        /// <typeparam name="TType"></typeparam>
        /// <returns></returns>
        public static QueryResult QueryObject<TType>(this TType context, QueryRequest request, ISchemaProvider schemaProvider, params object[] mutationArgs)
        {
            return QueryObject(context, request, schemaProvider, null, null, false, mutationArgs);
        }
        /// <summary>
        /// Extension method to query an object purely based on the a defined schema of that object.
        /// </summary>
        /// <param name="context">The root of your object graph you are querying. E.g. a DbContext</param>
        /// <param name="query">GraphQL query</param>
        /// <param name="schemaProvider">Schema definition. Defines new fields/entities. Maps names, etc.</param>
        /// <param name="claims">Security claims the user making the request has. These are checked against any fields/mutations that have the [Authorize] attribute set.</param>
        /// <typeparam name="TType"></typeparam>
        /// <returns></returns>
        public static QueryResult QueryObject<TType>(this TType context, QueryRequest request, ISchemaProvider schemaProvider, ClaimsIdentity claims, params object[] mutationArgs)
        {
            return QueryObject(context, request, schemaProvider, claims, null, false, mutationArgs);
        }
        /// <summary>
        /// Extension method to query an object purely based on the a defined schema of that object.
        /// </summary>
        /// <param name="context">The root of your object graph you are querying. E.g. a DbContext</param>
        /// <param name="query">GraphQL query</param>
        /// <param name="schemaProvider">Schema definition. Defines new fields/entities. Maps names, etc.</param>
        /// <param name="claims">Security claims the user making the request has. These are checked against any fields/mutations that have the [Authorize] attribute set.</param>
        /// <param name="methodProvider">Extend the query language with methods</param>
        /// <param name="includeDebugInfo">Include debug/timing information in the result</param>
        /// <typeparam name="TType"></typeparam>
        /// <returns></returns>
        public static QueryResult QueryObject<TType>(this TType context, string query, ISchemaProvider schemaProvider, ClaimsIdentity claims, IMethodProvider methodProvider = null, bool includeDebugInfo = false, params object[] mutationArgs)
        {
            return QueryObject(context, new QueryRequest { Query = query }, schemaProvider, claims, methodProvider, includeDebugInfo, mutationArgs);
        }

        /// <summary>
        /// Extension method to query an object purely based on the a defined schema of that object.
        /// </summary>
        /// <param name="context">The root of your object graph you are querying. E.g. a DbContext</param>
        /// <param name="request">GraphQL request object</param>
        /// <param name="schemaProvider">Schema definition. Defines new fields/entities. Maps names, etc.</param>
        /// <param name="claims">Security claims the user making the request has. These are checked against any fields/mutations that have the [Authorize] attribute set.</param>
        /// <param name="methodProvider">Extend the query language with methods</param>
        /// <param name="includeDebugInfo">Include debug/timing information in the result</param>
        /// <typeparam name="TType"></typeparam>
        /// <returns></returns>
        public static QueryResult QueryObject<TType>(this TType context, QueryRequest request, ISchemaProvider schemaProvider, ClaimsIdentity claims, IMethodProvider methodProvider = null, bool includeDebugInfo = false, params object[] mutationArgs)
        {
            if (methodProvider == null)
                methodProvider = new DefaultMethodProvider();
            Stopwatch timer = null;
            if (includeDebugInfo)
            {
                timer = new Stopwatch();
                timer.Start();
            }

            QueryResult result;
            try
            {
                var graphQLCompiler = new GraphQLCompiler(schemaProvider, methodProvider);
                var queryResult = graphQLCompiler.Compile(request, claims);
                result = queryResult.ExecuteQuery(context, request.OperationName, mutationArgs);
            }
            catch (Exception ex)
            {
                // error with the whole query
                result = new QueryResult {Errors = { new GraphQLError(ex.InnerException != null ? ex.InnerException.Message : ex.Message) }};
            }
            if (includeDebugInfo && timer != null)
            {
                timer.Stop();
                result.SetDebug(new { TotalMilliseconds = timer.ElapsedMilliseconds });
            }

            return result;
        }
    }
}
