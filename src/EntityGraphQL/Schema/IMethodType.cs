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
        string ReturnTypeClrSingle { get; }
        bool ReturnTypeNotNullable { get; }
        bool ReturnElementTypeNullable { get; }

        ArgType GetArgumentType(string argName);
        bool HasArgumentByName(string argName);
    }

    public class ArgType
    {
        public Type Type { get; set; }
        public bool TypeNotNullable { get; set; }
    }
}