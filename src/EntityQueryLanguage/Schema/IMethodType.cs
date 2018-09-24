using System;

namespace EntityQueryLanguage.Schema
{
    public interface IMethodType
    {
        Type GetArgumentType(string argName);
    }
}