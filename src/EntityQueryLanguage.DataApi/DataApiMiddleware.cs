using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace EntityQueryLanguage.DataApi
{
	public class DataApiMiddlewareOptions {
		public ISchemaProvider Schema { get; set; }
		public IMethodProvider MethodProvider { get; set; }
		public string Path { get; set; }
		public IDataApiRequestListener RequestListener { get; set; }
	}
	public class DataApiMiddleware<TContextType> where TContextType : IDisposable {
		private RequestDelegate _next;
		private ISchemaProvider _schemProvider;
		private IMethodProvider _methodProvider;
		private string _path = string.Empty;
		private DataManager<TContextType> _dataManager;

		public DataApiMiddleware(RequestDelegate next, DataApiMiddlewareOptions options, Func<TContextType> newDataContextFunc) {
			if (options == null)
				throw new System.ArgumentNullException("options");
			if (string.IsNullOrEmpty(options.Path))
				throw new System.ArgumentException("Please provide a path to the DataQueryMiddlewareOptions");
			_next = next;
			_schemProvider = options.Schema;
			_methodProvider = options.MethodProvider ?? new DefaultMethodProvider();
			_dataManager = new DataManager<TContextType>(newDataContextFunc);
			_path = options.Path;
		}

		public async Task Invoke(HttpContext context) {
			// check it matches our path, if we have one
			if (context.Request.Path.Value.StartsWith(_path)) {
				// right now ignore anything after our path

				if (context.Request.Method == "GET" || (context.Request.Method == "POST" && context.Request.Path.Value == _path + "/query")) {
					// a POST should be an add, but the query might be too long for a GET URL param
					// we process a POST to /{_path}/query as a GET with the body as the query instead of a URL param
					var timer = new System.Diagnostics.Stopwatch();
					timer.Start();

					var query = context.Request.Query["query"];
					if (string.IsNullOrEmpty(query)) {
						query = context.Request.Body.ToString();
					}
					var data = _dataManager.Query(query, _schemProvider, _methodProvider);
					timer.Stop();
					data.Add("_debug", new { TotalMilliseconds = timer.ElapsedMilliseconds });
					// TODO add support for requesting different data formats
					// for now it's JSON
					try {
						var resultData = JsonConvert.SerializeObject(data, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
						await context.Response.WriteAsync(resultData);
					}
					catch (Exception ex) {
						Console.WriteLine(ex);
						await context.Response.WriteAsync("{\"error\": \"" + ex.Message + "\"}");
					}
				}
				else {
					await context.Response.WriteAsync(string.Format("We don't currently support {0} at {1}.", context.Request.Method, context.Request.Path.Value));
				}
			} else {
				await _next(context);
			}
		}
	}
}
