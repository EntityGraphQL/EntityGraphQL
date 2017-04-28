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
            var rootTypes = CreateFieldsFromObjectAsSchema(contextType);
            BuildSchema(contextType, rootTypes);
        }

        private List<Field> CreateFieldsFromObjectAsSchema(Type type)
        {
            var fields = new List<Field>();
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
            return fields;
        }

        private void CacheType(Type propType)
        {
            if (propType.GetTypeInfo().IsGenericType && propType.IsEnumerable())
            {
                var genType = propType.GetGenericArguments()[0];
                if (!HasType(genType.Name) && genType.Name != "String" && (genType.GetTypeInfo().IsClass || genType.GetTypeInfo().IsInterface))
                {
                    // var fields = new List<Field>();
                    // add type before we recurse more that may also add the type
                    _types.Add(genType.Name, new EqlType(genType, genType.Name, ""));
                    CreateFieldsFromObjectAsSchema(genType);
                }
            }
            else if (!HasType(propType.Name) && propType.Name != "String" && (propType.GetTypeInfo().IsClass || propType.GetTypeInfo().IsInterface))
            {
                // var fields = new List<Field>();
                _types.Add(propType.Name, new EqlType(propType, propType.Name, ""));
                CreateFieldsFromObjectAsSchema(propType);
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
