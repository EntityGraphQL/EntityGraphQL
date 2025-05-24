using System.Collections.Generic;

namespace EntityGraphQL.Schema;
public interface IPropertySetTrackingDto
{
    bool IsSet(string propertyName);
    void MarkAsSet(IEnumerable<string> propertiesName);
    void MarkAsSet(string propertyName);
}