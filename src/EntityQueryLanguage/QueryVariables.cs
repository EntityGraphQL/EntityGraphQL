using System;
using System.Collections.Generic;

namespace EntityQueryLanguage
{
    public class QueryVariables : Dictionary<string, object>
    {
        public object GetValueFor(string varKey)
        {
            return ContainsKey(varKey) ? this[varKey] : null;
        }
    }
}