using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.IO;

namespace EntityQueryLanguage.DataApi
{
    public class DataApiMiddlewareOptions<TContextType>
    {
        public ISchemaProvider Schema { get; set; }
        public IMethodProvider MethodProvider { get; set; }
        public Func<TContextType> NewContextFunc { get; set; }
        public string Path { get; set; }
        public IDataApiRequestListener RequestListener { get; set; }
        public IRelationHandler RelationHandler { get; set; }
    }

    public static class DataApiMiddlewareExtension
    {
        public static void UseEql<TContextType>(this IApplicationBuilder app, string path, ISchemaProvider schemaProvider, Func<TContextType> newContextFunc, IRelationHandler customRelationHandler = null) where TContextType : IDisposable
        {
            var options = new DataApiMiddlewareOptions<TContextType> {
                Schema = schemaProvider,
                NewContextFunc = newContextFunc,
                Path = path,
                RelationHandler = customRelationHandler
            };
            app.UseMiddleware<DataApiMiddleware<TContextType>>(options);
        }
    }

    public class DataApiMiddleware<TContextType> where TContextType : IDisposable
    {
        private RequestDelegate _next;
        private ISchemaProvider _schemProvider;
        private IMethodProvider _methodProvider;
        private string _path = string.Empty;
        private DataManager<TContextType> _dataManager;
        private IRelationHandler _relationHandler;

        public DataApiMiddleware(RequestDelegate next, DataApiMiddlewareOptions<TContextType> options)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            if (string.IsNullOrEmpty(options.Path))
                throw new ArgumentException("Please provide a path to the DataQueryMiddlewareOptions");
            if (options.Schema == null)
                throw new ArgumentException("Please provide a schema to the DataQueryMiddlewareOptions");
            if (options.NewContextFunc == null)
                throw new ArgumentException("Please provide a NewContextFunc to the DataQueryMiddlewareOptions");
            _next = next;
            _schemProvider = options.Schema;
            _methodProvider = options.MethodProvider ?? new DefaultMethodProvider();
            _relationHandler = options.RelationHandler;
            _dataManager = new DataManager<TContextType>(options.NewContextFunc);
            _path = options.Path;
        }

        public async Task Invoke(HttpContext context)
        {
            // check it matches our path, if we have one
            if (context.Request.Path.Value.StartsWith(_path))
            {
                // right now ignore anything after our path

                if (context.Request.Method == "GET" || (context.Request.Method == "POST" && context.Request.Path.Value.TrimEnd('/') == _path.TrimEnd('/')))
                {
                    // a POST should be an add, but the query might be too long for a GET URL param
                    // we process a POST to /{_path} as a GET with the body as the query instead of a URL param
                    var query = context.Request.Query["q"];
                    if (string.IsNullOrEmpty(query))
                    {
                        using (var sr = new StreamReader(context.Request.Body))
                            query = sr.ReadToEnd();
                    }
                    var data = _dataManager.Query(query, _schemProvider, _methodProvider, _relationHandler);
                    // TODO add support for requesting different data formats
                    // for now it's JSON
                    context.Response.Headers.Add("Content-Type", "application/json");

                    try
                    {
                        var resultData = JsonConvert.SerializeObject(data, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
                        await context.Response.WriteAsync(resultData);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        await context.Response.WriteAsync("{\"error\": \"" + ex.Message + "\"}");
                    }
                }
                else
                {
                    await context.Response.WriteAsync(string.Format("We don't currently support {0} at {1}.", context.Request.Method, context.Request.Path.Value));
                }
            }
            else
            {
                await _next(context);
            }
        }
    }
}
