using System;
using System.Collections.Generic;

namespace EntityGraphQL
{
	/// <summary>
	/// A GraphQL request. The query, and any variables
	/// </summary>
	public class QueryRequest
	{
		// Name of the query or mutation you want to run in the Query (if it contains many)
		public string OperationName { get; set; }
		/// <summary>
		/// GraphQL query document
		/// </summary>
		/// <value></value>
		public string Query { get; set; }
		public QueryVariables Variables { get; set; }
	}

	/// <summary>
	/// Holds the variables passed along with a GraphQL query
	/// </summary>
	/// <typeparam name="string"></typeparam>
	/// <typeparam name="object"></typeparam>
	public class QueryVariables : Dictionary<string, object>
	{
		public object GetValueFor(string varKey)
		{
			return ContainsKey(varKey) ? this[varKey] : null;
		}
	}

	/// <summary>
	/// Describes any errors that might happen while resolving the query request
	/// </summary>
	public class GraphQLException : Exception
	{
		public GraphQLException(string message)
			: base(message)
		{
		}

	}
}