using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Reflection;
using EntityQueryLanguage.Extensions;

namespace EntityQueryLanguage
{
  /// A simple schema provider to map a EntityQL query directly to a object graph
  public class ObjectSchemaProvider<TContextType> : ISchemaProvider
  {
    private Type _contextType;
    private Func<object> _newContextFunc;
    private Dictionary<string, List<EqlPropertyInfo>> _propertiesOrFieldsByType = new Dictionary<string, List<EqlPropertyInfo>>(StringComparer.OrdinalIgnoreCase);
    
    public Type ContextType { get { return _contextType; } }
    
    public ObjectSchemaProvider(Func<object> newContextFunc = null) {
      _contextType = typeof(TContextType);
      _newContextFunc = newContextFunc;
      
      CacheFieldsFromObjectAsSchema(_contextType);
    }
    public bool EntityHasField(Type type, string identifier) {
      return TypeHasField(type.Name, identifier);
    }
    public bool TypeHasField(string typeName, string identifier) {
      return _propertiesOrFieldsByType.ContainsKey(typeName) && _propertiesOrFieldsByType[typeName].Any(c => c.LowerCaseName == identifier.ToLower());
    }
    public string GetActualFieldName(Type type, string identifier) {
      return GetActualFieldName(type.Name, identifier);
    }
    public string GetActualFieldName(string typeName, string identifier) {
      return _propertiesOrFieldsByType.ContainsKey(typeName) ? _propertiesOrFieldsByType[typeName].First(c => c.LowerCaseName == identifier.ToLower()).ActualName : string.Empty;
    }
    public Expression GetExpressionForField(Expression context, string typeName, string field) {
      return Expression.PropertyOrField(context, field);
    }
    
    public string GetSchemaTypeNameForRealType(Type type) {
      return type.Name;
    }
    
    private void CacheFieldsFromObjectAsSchema(Type type) {
      // cache fields/properties
      var fieldsForType = new List<EqlPropertyInfo>();
      _propertiesOrFieldsByType.Add(type.Name, fieldsForType);
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
          && !_propertiesOrFieldsByType.ContainsKey(propType.GetGenericArguments()[0].Name) && propType.GetGenericArguments()[0].Name != "String" 
          && (propType.GetGenericArguments()[0].GetTypeInfo().IsClass || propType.GetGenericArguments()[0].GetTypeInfo().IsInterface)) {
        CacheFieldsFromObjectAsSchema(propType.GetGenericArguments()[0]);
      }
      else if (!_propertiesOrFieldsByType.ContainsKey(propType.Name) && propType.Name != "String" && (propType.GetTypeInfo().IsClass || propType.GetTypeInfo().IsInterface)) {
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
