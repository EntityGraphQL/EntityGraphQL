using System;
using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL.Compiler;

public class EntityGraphQLValidationException : Exception
{
    public List<string> ValidationErrors { get; }

    public EntityGraphQLValidationException(IEnumerable<string> validationErrors)
    {
        ValidationErrors = validationErrors.Distinct().ToList();
    }

}