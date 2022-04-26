using System;
using System.Collections.Generic;

namespace EntityGraphQL.Compiler;

public class EntityGraphQLValidationException : Exception
{
    public List<string> ValidationErrors { get; }

    public EntityGraphQLValidationException(List<string> validationErrors)
    {
        ValidationErrors = validationErrors;
    }

}