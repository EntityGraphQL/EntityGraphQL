using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace EntityGraphQL
{
    public class QueryResult
    {
        [JsonProperty("errors")]
        public List<GraphQLError> Errors => (List<GraphQLError>)dataResults["errors"];
        [JsonProperty("data")]
        public ConcurrentDictionary<string, object> Data => (ConcurrentDictionary<string, object>)dataResults["data"];
        private readonly ConcurrentDictionary<string, object> dataResults = new ConcurrentDictionary<string, object>();

        public QueryResult()
        {
            dataResults["errors"] = new List<GraphQLError>();
            dataResults["data"] = new ConcurrentDictionary<string, object>();
        }

        internal void SetDebug(object debugData)
        {
            dataResults["_debug"] = debugData;
        }
    }
}