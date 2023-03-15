using System;

namespace EntityGraphQL
{
    public class EntityGraphQLArgumentException : Exception
    {
        public EntityGraphQLArgumentException(string message) : base(message) { }
        public EntityGraphQLArgumentException(string parameterName, string message) : base($"{message} (Parameter '{parameterName}')") { }
    }
}
