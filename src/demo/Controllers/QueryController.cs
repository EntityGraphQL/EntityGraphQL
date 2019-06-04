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
                /* Operation name was missing from the default "IntrospectionQuery" in the graphiql.js file.
                 * Manually added this ~approx line 2152 & 2168
                 * Update both fetcher like so: var fetch = observableToPromise(fetcher({ query: _introspectionQueries.introspectionQuery, operationName: "IntrospectionQuery" }));
                 */
                if (!string.IsNullOrEmpty(query.OperationName)
                    && (query.OperationName.Equals("IntrospectionQuery", StringComparison.InvariantCultureIgnoreCase)
                    || query.Query.Contains("IntrospectionQuery", StringComparison.InvariantCultureIgnoreCase)))
                {
                    var introspection = _schemaProvider.GetGraphQLIntrospectionSchema();
                    return introspection;
                }

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