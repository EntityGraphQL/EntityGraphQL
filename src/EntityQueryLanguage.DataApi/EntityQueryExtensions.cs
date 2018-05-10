using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using EntityQueryLanguage.DataApi.Parsing;
using System.Diagnostics;

namespace EntityQueryLanguage.DataApi
{
    public static class EntityQueryExtensions
    {
        /// <summary>
        /// Extension method to query an object purely based on the schema of that object.null Note it creates a new MappedSchemaProvider each time.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="dataQuery"></param>
        /// <returns></returns>
        public static IDictionary<string, object> QueryObject<TType>(this TType context, string dataQuery, bool includeDebugInfo = false)
        {
            return QueryObject(context, dataQuery, new MappedSchemaProvider<TType>(), null, null, includeDebugInfo);
        }
        /// Function that returns the DataContext for the queries. If null _serviceProvider is used
        public static IDictionary<string, object> QueryObject<TType>(this TType context, string dataQuery, ISchemaProvider schemaProvider, IRelationHandler relationHandler = null, IMethodProvider methodProvider = null, bool includeDebugInfo = false)
        {
            if (methodProvider == null)
                methodProvider = new DefaultMethodProvider();
            Stopwatch timer = null;
            if (includeDebugInfo)
            {
                timer = new Stopwatch();
                timer.Start();
            }

            var allData = new ConcurrentDictionary<string, object>();

            try
            {
                var objectGraph = new DataApiCompiler(schemaProvider, methodProvider, relationHandler).Compile(dataQuery);
                // Parallel.ForEach(objectGraph.Fields, node =>
                foreach (var node in objectGraph.Fields)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(node.Error))
                        {
                            System.Console.WriteLine(node.Error);
                            allData[node.Name] = node.Error;
                        }
                        else
                        {
                            var data = node.AsLambda().Compile().DynamicInvoke(context);
                            allData[node.Name] = data;
                        }
                    }
                    catch (Exception ex)
                    {
                        allData[node.Name] = new { eql_error = ex.Message };
                    }
                }
                // );
            }
            catch (Exception ex)
            {
                allData["error"] = ex.Message;
            }
            if (includeDebugInfo && timer != null)
            {
                timer.Stop();
                allData["_debug"] = new { TotalMilliseconds = timer.ElapsedMilliseconds };
            }

            return allData;
        }
    }
}
