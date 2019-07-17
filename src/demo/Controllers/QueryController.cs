using System;
using System.Net;
using EntityGraphQL;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace demo.Controllers
{
    [Route("api/[controller]")]
    public class QueryController : Controller
    {
        private readonly DemoContext _dbContext;
        private readonly MappedSchemaProvider<DemoContext> _schemaProvider;

        public QueryController(DemoContext dbContext, MappedSchemaProvider<DemoContext> schemaProvider)
        {
            this._dbContext = dbContext;
            this._schemaProvider = schemaProvider;
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
                var data = _dbContext.QueryObject(query, _schemaProvider);
                return data;
            }
            catch (Exception)
            {
                return HttpStatusCode.InternalServerError;
            }
        }
    }
}