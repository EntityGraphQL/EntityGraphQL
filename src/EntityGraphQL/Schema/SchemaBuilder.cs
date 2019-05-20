using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Reflection;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using Humanizer;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// A simple schema provider to automattically create a query schema based on an object.
    /// Commonly used with a DbContext.
    /// </summary>
    public class SchemaBuilder
    {
        private static readonly HashSet<string> ignoreProps = new HashSet<string> {
            "Database",
            "Model",
            "ChangeTracker"
        };

        private static readonly HashSet<string> ignoreTypes = new HashSet<string> {
            "String",
            "Byte[]"
        };

        /// <summary>
        /// Given the type TContextType recursively create a query schema based on the public properties of the object.
        /// </summary>
        /// <param name="autoCreateIdArguments">If True, automatically create a field for any root array thats context object contains an Id property. I.e. If Actor has an Id property and the root TContextType contains IEnumerable<Actor> Actors. A root field Actor(id) will be created.</param>
        /// <typeparam name="TContextType"></typeparam>
        /// <returns></returns>
        public static MappedSchemaProvider<TContextType> FromObject<TContextType>(bool autoCreateIdArguments = true)
        {
            var schema = new MappedSchemaProvider<TContextType>();
            var contextType = typeof(TContextType);
            var rootFields = AddFieldsFromObjectToSchema<TContextType>(contextType, schema);
            foreach (var f in rootFields)
            {
                if (autoCreateIdArguments)
                {
                    // add non-pural field with argument of ID
                    AddFieldWithIdArgumentIfExists(schema, contextType, f);
                }
                schema.AddField(f);
            }
            return schema;
        }

        private static void AddFieldWithIdArgumentIfExists<TContextType>(MappedSchemaProvider<TContextType> schema, Type contextType, Field fieldProp)
        {
            if (!fieldProp.Resolve.Type.IsEnumerableOrArray())
                return;
            var schemaType = schema.Type(fieldProp.ReturnTypeSingle);
            var idFieldDef = schemaType.GetFields().FirstOrDefault(f => f.Name == "Id");
            if (idFieldDef == null)
                return;

            // We need to build an anonymous type with id = RequiredField<idFieldDef.Resolve.Type>()
            // Resulting lambda is (a, p) => a.Where(b => b.Id == p.Id).First()
            // This allows us to "insert" .Select() (and .Include()) before the .First()
            var requiredFieldType = typeof(RequiredField<>).MakeGenericType(idFieldDef.Resolve.Type);
            var fieldNameAndType = new Dictionary<string, Type> { { "id", requiredFieldType } };
            var argTypes = LinqRuntimeTypeBuilder.GetDynamicType(fieldNameAndType);
            var argTypesValue = argTypes.GetTypeInfo().GetConstructors()[0].Invoke(new Type[0]);
            var argTypeParam = Expression.Parameter(argTypes);
            Type arrayContextType = schema.Type(fieldProp.ReturnTypeSingle).ContextType;
            var arrayContextParam = Expression.Parameter(arrayContextType);
            var ctxId = Expression.PropertyOrField(arrayContextParam, "Id");
            Expression argId = Expression.PropertyOrField(argTypeParam, "id");
            argId = Expression.Property(argId, "Value"); // call RequiredField<>.Value to get the real type without a convert
            var idBody = Expression.MakeBinary(ExpressionType.Equal, ctxId, argId);
            var idLambda = Expression.Lambda(idBody, new[] { arrayContextParam });
            Expression body = ExpressionUtil.MakeExpressionCall(new[] { typeof(Queryable), typeof(Enumerable) }, "Where", new Type[] { arrayContextType }, fieldProp.Resolve, idLambda);

            body = ExpressionUtil.MakeExpressionCall(new[] { typeof(Queryable), typeof(Enumerable) }, "FirstOrDefault", new Type[] { arrayContextType }, body);
            var contextParam = Expression.Parameter(contextType);
            var lambdaParams = new[] { contextParam, argTypeParam };
            body = new ParameterReplacer().ReplaceByType(body, contextType, contextParam);
            var selectionExpression = Expression.Lambda(body, lambdaParams);
            var name = fieldProp.Name.Singularize();
            if (name == null)
            {
                // If we can't singularize it just use the name plus something as GraphQL doesn't support field overloads
                name = $"{fieldProp.Name}ById";
            }
            var field = new Field(name, selectionExpression, $"Return a {fieldProp.ReturnTypeSingle} by its Id", fieldProp.ReturnTypeSingle, argTypesValue);
            schema.AddField(field);
        }

        private static List<Field> AddFieldsFromObjectToSchema<TContextType>(Type type, MappedSchemaProvider<TContextType> schema)
        {
            var fields = new List<Field>();
            // cache fields/properties
            var param = Expression.Parameter(type);
            if (type.IsArray || type.IsEnumerableOrArray())
                return fields;

            foreach (var prop in type.GetProperties())
            {
                if (ignoreProps.Contains(prop.Name) || prop.GetCustomAttribute(typeof(GraphQLIgnoreAttribute)) != null)
                {
                    continue;
                }
                LambdaExpression le = Expression.Lambda(Expression.Property(param, prop.Name), param);
                var f = new Field(prop.Name, le, "");
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
            if (propType.IsEnumerableOrArray())
            {
                propType = propType.GetEnumerableOrArrayType();
            }

            if (!schema.HasType(propType.Name) && !ignoreTypes.Contains(propType.Name) && (propType.GetTypeInfo().IsClass || propType.GetTypeInfo().IsInterface))
            {
                // add type before we recurse more that may also add the type
                // dynamcially call generic method
                var parameters = new List<Expression> {Expression.Constant(propType.Name), Expression.Constant(""), Expression.Constant(null)};
                // hate this, but want to build the types with the right Genenics so you can extend them later.
                // this is not the fastest, but only done on schema creation
                var method = schema.GetType().GetMethod("AddType", new [] {typeof(string), typeof(string)});
                method = method.MakeGenericMethod(propType);
                var t = (ISchemaType)method.Invoke(schema, new object[] { propType.Name, propType.Name + " description" });

                var fields = AddFieldsFromObjectToSchema<TContextType>(propType, schema);
                t.AddFields(fields);
            }
        }
    }
}
