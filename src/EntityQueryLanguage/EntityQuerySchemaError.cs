using System;

namespace EntityQueryLanguage
{
    public class EntityQuerySchemaError : Exception
    {
        public EntityQuerySchemaError()
        {
        }

        public EntityQuerySchemaError(string message) : base(message)
        {
        }

        public EntityQuerySchemaError(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}