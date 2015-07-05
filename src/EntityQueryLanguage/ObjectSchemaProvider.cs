using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using EntityQueryLanguage.Extensions;

namespace EntityQueryLanguage
{
  /// A simple schema provider to map a EntityQL query directly to a object graph
  public class ObjectSchemaProvider : ISchemaProvider
  {
    private Type _contextType;
    private Func<object> _newContextFunc;
    private Dictionary<Type, List<EqlPropertyInfo>> _propertiesOrFieldsByType = new Dictionary<Type, List<EqlPropertyInfo>>();
    
    public Type ContextType { get { return _contextType; } }
    
    public ObjectSchemaProvider(Type contextType, Func<object> newContextFunc = null) {
      _contextType = contextType;
      _newContextFunc = newContextFunc;
      
      CacheFieldsFromObjectAsSchema(_contextType);
    }
    
    public bool EntityTypeHasField(Type type, string identifier) {
      return _propertiesOrFieldsByType.ContainsKey(type) && _propertiesOrFieldsByType[type].Any(c => c.LowerCaseName == identifier.ToLower());
    }
    public string GetActualFieldName(Type type, string identifier) {
      return _propertiesOrFieldsByType.ContainsKey(type) ? _propertiesOrFieldsByType[type].First(c => c.LowerCaseName == identifier.ToLower()).ActualName : string.Empty;
    }
    public TContextType CreateContextValue<TContextType>() {
      return (TContextType)CreateContextValue();
    }
    public object CreateContextValue() {
      if (_newContextFunc != null)
        return _newContextFunc();
      return Activator.CreateInstance(_contextType);
    }
    
    private void CacheFieldsFromObjectAsSchema(Type type) {
      // cache fields/properties
      var fieldsForType = new List<EqlPropertyInfo>();
      _propertiesOrFieldsByType.Add(type, fieldsForType);
      foreach (var prop in type.GetProperties()) {
        fieldsForType.Add(new EqlPropertyInfo(prop.Name, prop.Name.ToLower()));
        var propType = prop.PropertyType;
        CacheType(prop.PropertyType);
      }
      foreach (var prop in type.GetFields()) {
        fieldsForType.Add(new EqlPropertyInfo(prop.Name, prop.Name.ToLower()));
        CacheType(prop.FieldType);
      }
    }
    private void CacheType(Type propType) {
      if (propType.GetTypeInfo().IsGenericType && propType.IsEnumerable()  
          && !_propertiesOrFieldsByType.ContainsKey(propType.GetGenericArguments()[0]) && propType.GetGenericArguments()[0].Name != "String" 
          && (propType.GetGenericArguments()[0].GetTypeInfo().IsClass || propType.GetGenericArguments()[0].GetTypeInfo().IsInterface)) {
        CacheFieldsFromObjectAsSchema(propType.GetGenericArguments()[0]);
      }
      else if (!_propertiesOrFieldsByType.ContainsKey(propType) && propType.Name != "String" && (propType.GetTypeInfo().IsClass || propType.GetTypeInfo().IsInterface)) {
        CacheFieldsFromObjectAsSchema(propType);
      }
    }
    
    private class EqlPropertyInfo {
      public string ActualName {get; private set; }
      public string LowerCaseName {get; private set; }
      public EqlPropertyInfo(string actualName, string lowerCase) {
        ActualName = actualName;
        LowerCaseName = lowerCase;
      }
    }
  }
}
