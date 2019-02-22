using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace EntityGraphQL
{
    public class QueryResult
    {
        private readonly ConcurrentDictionary<string, object> data = new ConcurrentDictionary<string, object>();
        public List<GraphQLError> Errors => (List<GraphQLError>)data["errors"];
        public ConcurrentDictionary<string, object> Data => (ConcurrentDictionary<string, object>)data["data"];

        public QueryResult()
        {
            data["errors"] = new List<GraphQLError>();
            data["data"] = new ConcurrentDictionary<string, object>();
        }

        internal void SetDebug(object debugData)
        {
            data["_debug"] = debugData;
        }
    }
}