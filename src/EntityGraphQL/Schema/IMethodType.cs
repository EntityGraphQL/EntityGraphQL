using System;
using System.Collections.Generic;

namespace EntityGraphQL.Schema
{
    public interface IMethodType
    {
        IDictionary<string, ArgType> Arguments { get; }
        string Name { get; }
        Type ReturnTypeClr { get; }
        string Description { get; }
        /// <summary>
        /// Get the GQL return type of the field or call
        /// </summary>
        /// <param name="schema"></param>
        /// <returns></returns>
        string GetReturnType(ISchemaProvider schema);
        bool ReturnTypeNotNullable { get; }
        bool ReturnElementTypeNullable { get; }
        RequiredClaims AuthorizeClaims { get; }

        ArgType GetArgumentType(string argName);
        bool HasArgumentByName(string argName);
    }

    public class ArgType
    {
        public Type Type { get; set; }
        public bool TypeNotNullable { get; set; }
    }
}