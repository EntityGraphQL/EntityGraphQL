using System;
using System.Net;
using EntityQueryLanguage;
using EntityQueryLanguage.DataApi;
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
            return RunDataQuery(query);
        }

        [HttpPost]
        public object Post([FromBody]string query)
        {
            return RunDataQuery(query);
        }

        private object RunDataQuery(string query)
        {

            try
            {
                var schemProvider = new MappedSchemaProvider<DemoContext>();

                var data = _dbContext.QueryObject(query, _schemaProvider, relationHandler: new EfRelationHandler(typeof(EntityFrameworkQueryableExtensions)));
                return data;
            }
            catch (Exception)
            {
                return HttpStatusCode.InternalServerError;
            }
        }
    }
}