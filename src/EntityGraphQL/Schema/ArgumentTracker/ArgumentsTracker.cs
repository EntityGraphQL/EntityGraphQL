using System;
using System.Collections.Generic;

namespace EntityGraphQL.Schema;

public class ArgumentsTracker : IArgumentsTracker
{
    private readonly HashSet<string> setProperties = new(StringComparer.OrdinalIgnoreCase);

    public void MarkAsSet(string propertyName) => setProperties.Add(propertyName);

    public void MarkAsSet(IEnumerable<string> propertiesName)
    {
        foreach (var prop in propertiesName)
        {
            setProperties.Add(prop);
        }
    }

    public bool IsSet(string propertyName) => setProperties.Contains(propertyName);
}
