using System;

namespace EntityGraphQL
{
    internal sealed class EntityGraphQLFieldException : Exception
    {
        public readonly string FieldName;

        public EntityGraphQLFieldException(string fieldName, Exception innerException) : base(null, innerException)
        {
            FieldName = fieldName;
        }
    }
}
