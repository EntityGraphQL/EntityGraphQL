using System;

namespace EntityGraphQL.Schema
{
    public class AllowedException
    {
        private readonly Type exceptionType;
        private readonly bool exactMatch;

        public AllowedException(Type exceptionType, bool exactMatch = false)
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
