using System;
using System.Collections.Generic;

namespace EntityGraphQL.Schema
{
    public interface IMethodType
    {
        IDictionary<string, Type> Arguments { get; }
        bool IsEnumerable { get; }
        string Name { get; }
        Type ReturnTypeClr { get; }
        string Description { get; }

        Type GetArgumentType(string argName);
        bool HasArgumentByName(string argName);
    }
}