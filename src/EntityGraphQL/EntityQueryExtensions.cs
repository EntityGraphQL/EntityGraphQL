using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Diagnostics;
using EntityGraphQL.Schema;
using EntityGraphQL.Compiler;
using EntityGraphQL.LinqQuery;

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
        /// <param name="methodProvider">Extend the query language with methods</param>
        /// <param name="includeDebugInfo">Include debug/timing information in the result</param>
        /// <typeparam name="TType"></typeparam>
        /// <returns></returns>
        public static QueryResult QueryObject<TType>(this TType context, string query, ISchemaProvider schemaProvider, IMethodProvider methodProvider = null, bool includeDebugInfo = false)
        {
            return QueryObject(context, new QueryRequest { Query = query }, schemaProvider, methodProvider, includeDebugInfo);
        }

        /// <summary>
        /// Extension method to query an object purely based on the a defined schema of that object.
        /// </summary>
        /// <param name="context">The root of your object graph you are querying. E.g. a DbContext</param>
        /// <param name="request">GraphQL request object</param>
        /// <param name="schemaProvider">Schema definition. Defines new fields/entities. Maps names, etc.</param>
        /// <param name="methodProvider">Extend the query language with methods</param>
        /// <param name="includeDebugInfo">Include debug/timing information in the result</param>
        /// <typeparam name="TType"></typeparam>
        /// <returns></returns>
        public static QueryResult QueryObject<TType>(this TType context, QueryRequest request, ISchemaProvider schemaProvider, IMethodProvider methodProvider = null, bool includeDebugInfo = false)
        {
            if (methodProvider == null)
                methodProvider = new DefaultMethodProvider();
            Stopwatch timer = null;
            if (includeDebugInfo)
            {
                timer = new Stopwatch();
                timer.Start();
            }

            var result = new QueryResult();

            try
            {
                var graphQLCompiler = new GraphQLCompiler(schemaProvider, methodProvider);
                var queryResult = (GraphQLResultNode)graphQLCompiler.Compile(request);
                queryResult.Execute(context, result);
            }
            catch (Exception ex)
            {
                // error with the whole query
                result.Errors.Add(new GraphQLError(ex.Message));
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
