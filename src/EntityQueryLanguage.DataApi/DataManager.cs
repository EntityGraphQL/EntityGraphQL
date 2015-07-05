using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using EntityQueryLanguage.DataApi.Parsing;


namespace EntityQueryLanguage.DataApi {
  public class DataManager<TContextType> where TContextType : IDisposable
  {
    public DataManager() {
    }

    public IDictionary<string, object> Query(string dataQuery, ISchemaProvider schemaProvider, IMethodProvider methodProvider)
    {
      var allData = new ConcurrentDictionary<string, object>();

      try {
        var objectGraph = new DataApiCompiler(schemaProvider, methodProvider).Compile(dataQuery);
        var compiler = new EqlCompiler();
  
        Parallel.ForEach(objectGraph.Fields, node => {
          try {
            if (!string.IsNullOrEmpty(node.Error)) {
              System.Console.WriteLine(node.Error);
              allData[node.Name] = node.Error;
            }
            else {
              // fetch the data
              // Some EF code here...
              using (var ctx = schemaProvider.CreateContextValue<TContextType>()) {
                var data = node.AsLambda().Compile().DynamicInvoke(ctx);
                allData[node.Name] = data;
              }
            }
          }
          catch (Exception ex) {
            allData[node.Name] = new { Name = ex.Message };
          }
        });  
      }
      catch (Exception ex) {
        allData["error"] = ex.Message;
      }
      return allData;
    }
  }
}
