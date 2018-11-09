using System;

namespace EntityGraphQL.Schema
{
    public interface IMethodType
    {
        Type GetArgumentType(string argName);
    }
}