using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Reflection;
using EntityQueryLanguage.Extensions;

namespace EntityQueryLanguage
{
    /// A simple schema provider to map a EntityQL query directly to a object graph
    public class ObjectSchemaProvider<TContextType> : MappedSchemaProvider
    {
        public ObjectSchemaProvider()
        {
            var contextType = typeof(TContextType);
            var rootTypes = new List<Field>();
            CreateFieldsFromObjectAsSchema(contextType, rootTypes);
            BuildSchema(contextType, rootTypes);
        }

        private void CreateFieldsFromObjectAsSchema(Type type, List<Field> fields)
        {
            // cache fields/properties
            var param = Expression.Parameter(type);
            foreach (var prop in type.GetProperties())
            {
                LambdaExpression le = Expression.Lambda(Expression.Property(param, prop.Name), param);
                var f = new Field(le, prop.Name, "");
                f.Name = prop.Name;
                fields.Add(f);
                CacheType(prop.PropertyType);
            }
            foreach (var prop in type.GetFields())
            {
                LambdaExpression le = Expression.Lambda(Expression.Field(param, prop.Name), param);
                var f = new Field(le, prop.Name, "");
                f.Name = prop.Name;
                fields.Add(f);
                CacheType(prop.FieldType);
            }
        }

        private void CacheType(Type propType)
        {
            if (propType.GetTypeInfo().IsGenericType && propType.IsEnumerable()
                && !HasType(propType.GetGenericArguments()[0].Name) && propType.GetGenericArguments()[0].Name != "String"
                && (propType.GetGenericArguments()[0].GetTypeInfo().IsClass || propType.GetGenericArguments()[0].GetTypeInfo().IsInterface))
            {
                var rootTypes = new List<Field>();
                CreateFieldsFromObjectAsSchema(propType.GetGenericArguments()[0], rootTypes);
                var genType = propType.GetGenericArguments()[0];
                _types.Add(genType.Name, new EqlType(genType, genType.Name, "", rootTypes));
            }
            else if (!HasType(propType.Name) && propType.Name != "String" && (propType.GetTypeInfo().IsClass || propType.GetTypeInfo().IsInterface))
            {
                var rootTypes = new List<Field>();
                CreateFieldsFromObjectAsSchema(propType, rootTypes);
                _types.Add(propType.Name, new EqlType(propType, propType.Name, "", rootTypes));
            }
        }

        public void ExtendType<TBaseType>(string fieldName, Expression<Func<TBaseType, object>> fieldFunc, string description = "")
        {
            var f = new Field(fieldFunc, description, fieldFunc.ReturnType.Name);
            f.Name = fieldName;
            _types[typeof(TBaseType).Name].AddField(f);
        }
    }
}
