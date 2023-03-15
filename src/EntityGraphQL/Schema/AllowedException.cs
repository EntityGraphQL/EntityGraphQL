using System;

namespace EntityGraphQL.Schema
{
#pragma warning disable CA1711
    public class AllowedException
#pragma warning restore CA1711
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
