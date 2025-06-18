using System.Collections.Generic;

namespace EntityGraphQL.Schema;

public interface IArgumentsTracker
{
    /// <summary>
    /// True if the property was set by the user. Let's you know if the property is just a default dotnet property or
    /// was set to that value by the user. E.g. null
    /// </summary>
    bool IsSet(string propertyName);
    void MarkAsSet(IEnumerable<string> propertiesName);
    void MarkAsSet(string propertyName);
}
