using System;
using System.Net;
using EntityGraphQL;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Mvc;

namespace demo.Controllers
{
    [Route("api/[controller]")]
    public class QueryController : Controller
    {
        private readonly DemoContext _dbContext;
        private readonly SchemaProvider<DemoContext, IServiceProvider> _schemaProvider;
        private readonly IServiceProvider _serviceProvider;

        public QueryController(DemoContext dbContext, SchemaProvider<DemoContext, IServiceProvider> schemaProvider, IServiceProvider serviceProvider)
        {
            this._dbContext = dbContext;
            this._schemaProvider = schemaProvider;
            this._serviceProvider = serviceProvider;
        }

        [HttpGet]
        public object Get(string query)
        {
            return RunDataQuery(new QueryRequest { Query = query });
        }

        [HttpPost]
        public object Post([FromBody]QueryRequest query)
        {
            return RunDataQuery(query);
        }

        private object RunDataQuery(QueryRequest query)
        {
            try
            {
                // _serviceProvider is passed to mutations and field selections at run time which opens a lot of flexibility
                // last argument can be claims to implement security checks
                var data = _schemaProvider.ExecuteQuery(query, _dbContext, _serviceProvider, null);
                return data;
            }
            catch (Exception)
            {
                return HttpStatusCode.InternalServerError;
            }
        }
    }
}