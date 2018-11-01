using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using EntityQueryLanguage.GraphQL.Parsing;
using System.Diagnostics;
using EntityQueryLanguage.Schema;
using EntityQueryLanguage.Compiler;

namespace EntityQueryLanguage.GraphQL
{
    public static class EntityQueryExtensions
    {
        /// <summary>
        /// Extension method to query an object purely based on the schema of that object. Note it creates a new MappedSchemaProvider each time.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="dataQuery"></param>
        /// <returns></returns>
        public static IDictionary<string, object> QueryObject<TType>(this TType context, string query, ISchemaProvider schemaProvider, IRelationHandler relationHandler = null, IMethodProvider methodProvider = null, bool includeDebugInfo = false)
        {
            return QueryObject(context, new GraphQLRequest { Query = query }, schemaProvider, relationHandler, methodProvider, includeDebugInfo);
        }

        /// Function that returns the DataContext for the queries. If null _serviceProvider is used
        public static IDictionary<string, object> QueryObject<TType>(this TType context, GraphQLRequest request, ISchemaProvider schemaProvider, IRelationHandler relationHandler = null, IMethodProvider methodProvider = null, bool includeDebugInfo = false)
        {
            if (methodProvider == null)
                methodProvider = new DefaultMethodProvider();
            Stopwatch timer = null;
            if (includeDebugInfo)
            {
                timer = new Stopwatch();
                timer.Start();
            }

            var queryData = new ConcurrentDictionary<string, object>();
            var result = new Dictionary<string, object>();
            var errors = new List<GraphQLError>();

            try
            {
                var objectGraph = new GraphQLCompiler(schemaProvider, methodProvider, relationHandler).Compile(request);
                foreach (var node in objectGraph.Fields.Where(f => f.IsMutation))
                {
                    ExecuteNode(context, request, queryData, node);
                }
                // Parallel.ForEach(objectGraph.Fields, node =>
                foreach (var node in objectGraph.Fields.Where(f => !f.IsMutation))
                {
                    ExecuteNode(context, request, queryData, node);
                }
                // );
            }
            catch (Exception ex)
            {
                // error with the whole query
                errors.Add(new GraphQLError(ex.Message));
            }
            if (includeDebugInfo && timer != null)
            {
                timer.Stop();
                result["_debug"] = new { TotalMilliseconds = timer.ElapsedMilliseconds };
            }
            result["data"] = queryData;
            result["errors"] = errors;

            return result;
        }

        private static void ExecuteNode<TType>(TType context, GraphQLRequest request, ConcurrentDictionary<string, object> queryData, IGraphQLNode node)
        {
            queryData[node.Name] = null;
            // request.Variables are already compiled into the expression
            var data = node.Execute(context);
            queryData[node.Name] = data;
        }
    }
}
