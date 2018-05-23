using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Reflection;
using EntityQueryLanguage.Extensions;
using EntityQueryLanguage.Schema;

namespace EntityQueryLanguage
{
    /// A simple schema provider to map a EntityQL query directly to a object graph
    public class SchemaBuilder
    {
        public static MappedSchemaProvider<TContextType> FromObject<TContextType>()
        {
            var schema = new MappedSchemaProvider<TContextType>();
            var contextType = typeof(TContextType);
            var rootFields = CreateFieldsFromObjectAsSchema<TContextType>(contextType, schema);
            foreach (var f in rootFields)
            {
                schema.AddField(f);
            }
            return schema;
        }

        private static List<Field> CreateFieldsFromObjectAsSchema<TContextType>(Type type, MappedSchemaProvider<TContextType> schema)
        {
            var fields = new List<Field>();
            // cache fields/properties
            var param = Expression.Parameter(type);
            foreach (var prop in type.GetProperties())
            {
                LambdaExpression le = Expression.Lambda(Expression.Property(param, prop.Name), param);
                var f = new Field(prop.Name, le, prop.Name);
                fields.Add(f);
                CacheType<TContextType>(prop.PropertyType, schema);
            }
            foreach (var prop in type.GetFields())
            {
                LambdaExpression le = Expression.Lambda(Expression.Field(param, prop.Name), param);
                var f = new Field(prop.Name, le, prop.Name);
                fields.Add(f);
                CacheType<TContextType>(prop.FieldType, schema);
            }
            return fields;
        }

        private static void CacheType<TContextType>(Type propType,  MappedSchemaProvider<TContextType> schema)
        {
            if (propType.GetTypeInfo().IsGenericType && propType.IsEnumerable())
            {
                propType = propType.GetGenericArguments()[0];
            }

            if (!schema.HasType(propType.Name) && propType.Name != "String" && (propType.GetTypeInfo().IsClass || propType.GetTypeInfo().IsInterface))
            {
                // add type before we recurse more that may also add the type
                // dynamcially call generic method
                var parameters = new List<Expression> {Expression.Constant(propType.Name), Expression.Constant(""), Expression.Constant(null)};
                // hate this, but want to build the types with the right Genenics so you can extend them later.
                // this is not the fastest, but only done on schema creation
                var method = schema.GetType().GetMethod("AddType", new [] {typeof(string), typeof(string)});
                method = method.MakeGenericMethod(propType);
                var t = (IEqlType)method.Invoke(schema, new object[] { propType.Name, propType.Name + " description" });

                var fields = CreateFieldsFromObjectAsSchema<TContextType>(propType, schema);
                t.AddFields(fields);
            }
        }
    }
}
