using System;
using System.Collections.Generic;

namespace EntityGraphQL.Schema;

public class ArgumentsTracker : IArgumentsTracker
{
    private readonly HashSet<string> setProperties = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> setOrder = [];

    public void MarkAsSet(string propertyName)
    {
        if (setProperties.Add(propertyName))
            setOrder.Add(propertyName);
    }

    public void MarkAsSet(IEnumerable<string> propertiesName)
    {
        foreach (var prop in propertiesName)
        {
            MarkAsSet(prop);
        }
    }

    public bool IsSet(string propertyName) => setProperties.Contains(propertyName);

    /// <summary>
    /// The properties set by the user, in the order they were set - the order they were written in the query
    /// document or the variables. A method rather than a property so it is never picked up as a schema field.
    /// </summary>
    public IReadOnlyList<string> GetSetPropertiesInOrder() => setOrder;
}
