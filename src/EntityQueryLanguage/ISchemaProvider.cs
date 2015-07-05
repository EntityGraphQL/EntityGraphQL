using System;

namespace EntityQueryLanguage
{
  public interface ISchemaProvider
  {
    Type ContextType { get; }
    bool EntityTypeHasField(Type type, string identifier);
    string GetActualFieldName(Type type, string identifier);
    TContextType CreateContextValue<TContextType>();
    object CreateContextValue();
  }
}
