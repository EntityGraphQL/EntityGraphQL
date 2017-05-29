using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using EntityQueryLanguage.DataApi.Parsing;


namespace EntityQueryLanguage.DataApi
{
    public class DataManager<TContextType> where TContextType : IDisposable
    {
        /// Function that returns the DataContext for the queries. If null _serviceProvider is used
        public static IDictionary<string, object> Query(TContextType context, string dataQuery, ISchemaProvider schemaProvider, IMethodProvider methodProvider, IRelationHandler relationHandler = null)
        {
            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();

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
            timer.Stop();
            allData["_debug"] = new { TotalMilliseconds = timer.ElapsedMilliseconds };

            return allData;
        }
    }
}
