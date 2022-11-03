using System;

namespace EntityGraphQL.Schema
{
    public class WhitelistedException
    {
        private readonly Type exceptionType;
        private readonly bool exactMatch;

        public WhitelistedException(Type exceptionType, bool exactMatch = false)
        {
            this.exceptionType = exceptionType;
            this.exactMatch = exactMatch;
        }

        public bool IsAllowed(Exception ex)
        {
            if (exactMatch) return ex.GetType() == exceptionType;
            return exceptionType.IsInstanceOfType(ex);
        }
    }
}
