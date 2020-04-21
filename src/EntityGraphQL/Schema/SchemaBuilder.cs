using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Reflection;
using EntityGraphQL.Extensions;
using Humanizer;
using EntityGraphQL.Compiler.Util;
using System.ComponentModel;
using EntityGraphQL.Authorization;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// A simple schema provider to automattically create a query schema based on an object.
    /// Commonly used with a DbContext.
    /// </summary>
    public static class SchemaBuilder
    {
        private static readonly HashSet<string> ignoreProps = new HashSet<string> {
            "Database",
            "Model",
            "ChangeTracker",
            "ContextId"
        };

        private static readonly HashSet<string> ignoreTypes = new HashSet<string> {
            "String",
            "Byte[]"
        };


        public static SchemaProvider<TContext> Create<TContext>()
        {
            return new SchemaProvider<TContext>();
        }

        /// <summary>
        /// Given the type TContextType recursively create a query schema based on the public properties of the object.
        /// </summary>
        /// <param name="autoCreateIdArguments">If True, automatically create a field for any root array thats context object contains an Id property. I.e. If Actor has an Id property and the root TContextType contains IEnumerable<Actor> Actors. A root field Actor(id) will be created.</param>
        /// <typeparam name="TContextType"></typeparam>
        /// <returns></returns>
        public static SchemaProvider<TContextType> FromObject<TContextType>(bool autoCreateIdArguments = true, bool autoCreateEnumTypes = true)
        {
            var schema = new SchemaProvider<TContextType>();
            var contextType = typeof(TContextType);
            var rootFields = GetFieldsFromObject(contextType, schema, autoCreateEnumTypes);
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

        private static void AddFieldWithIdArgumentIfExists<TContextType>(SchemaProvider<TContextType> schema, Type contextType, Field fieldProp)
        {
            if (!fieldProp.Resolve.Type.IsEnumerableOrArray())
                return;
            var schemaType = schema.Type(fieldProp.GetReturnType(schema));
            var idFieldDef = schemaType.GetFields().FirstOrDefault(f => f.Name == "id");
            if (idFieldDef == null)
                return;

            // We need to build an anonymous type with id = RequiredField<idFieldDef.Resolve.Type>()
            // Resulting lambda is (a, p) => a.Where(b => b.Id == p.Id).First()
            // This allows us to "insert" .Select() (and .Include()) before the .First()
            var requiredFieldType = typeof(RequiredField<>).MakeGenericType(idFieldDef.Resolve.Type);
            var fieldNameAndType = new Dictionary<string, Type> { { "id", requiredFieldType } };
            var argTypes = LinqRuntimeTypeBuilder.GetDynamicType(fieldNameAndType);
            var argTypesValue = argTypes.GetTypeInfo().GetConstructors()[0].Invoke(new Type[0]);
            var argTypeParam = Expression.Parameter(argTypes, $"args_{argTypes.Name}");
            Type arrayContextType = schema.Type(fieldProp.GetReturnType(schema)).ContextType;
            var arrayContextParam = Expression.Parameter(arrayContextType, $"arrcxt_{arrayContextType.Name}");
            var ctxId = Expression.PropertyOrField(arrayContextParam, "Id");
            Expression argId = Expression.PropertyOrField(argTypeParam, "id");
            argId = Expression.Property(argId, "Value"); // call RequiredField<>.Value to get the real type without a convert
            var idBody = Expression.MakeBinary(ExpressionType.Equal, ctxId, argId);
            var idLambda = Expression.Lambda(idBody, new[] { arrayContextParam });
            Expression body = ExpressionUtil.MakeCallOnQueryable("Where", new Type[] { arrayContextType }, fieldProp.Resolve, idLambda);

            body = ExpressionUtil.MakeCallOnQueryable("FirstOrDefault", new Type[] { arrayContextType }, body);
            var contextParam = Expression.Parameter(contextType, $"cxt_{contextType.Name}");
            var lambdaParams = new[] { contextParam, argTypeParam };
            body = new ParameterReplacer().ReplaceByType(body, contextType, contextParam);
            var selectionExpression = Expression.Lambda(body, lambdaParams);
            var name = fieldProp.Name.Singularize();
            if (name == null)
            {
                // If we can't singularize it just use the name plus something as GraphQL doesn't support field overloads
                name = $"{fieldProp.Name}ById";
            }
            var field = new Field(name, selectionExpression, $"Return a {fieldProp.GetReturnType(schema)} by its Id", fieldProp.GetReturnType(schema), argTypesValue, fieldProp.AuthorizeClaims);
            schema.AddField(field);
        }

        public static List<Field> GetFieldsFromObject(Type type, ISchemaProvider schema, bool createEnumTypes, bool createNewComplexTypes = true)
        {
            var fields = new List<Field>();
            // cache fields/properties
            var param = Expression.Parameter(type, $"p_{type.Name}");
            if (type.IsArray || type.IsEnumerableOrArray())
                return fields;

            foreach (var prop in type.GetProperties())
            {
                var f = ProcessFieldOrProperty(prop, prop.PropertyType, param, schema, createEnumTypes, createNewComplexTypes);
                if (f != null)
                    fields.Add(f);
            }
            foreach (var prop in type.GetFields())
            {
                var f = ProcessFieldOrProperty(prop, prop.FieldType, param, schema, createEnumTypes, createNewComplexTypes);
                if (f != null)
                    fields.Add(f);
            }
            return fields;
        }

        private static Field ProcessFieldOrProperty(MemberInfo prop, Type fieldOrPropType, ParameterExpression param, ISchemaProvider schema, bool createEnumTypes, bool createNewComplexTypes)
        {
            if (ignoreProps.Contains(prop.Name) || GraphQLIgnoreAttribute.ShouldIgnoreMemberFromQuery(prop))
            {
                return null;
            }

            // Get Description from ComponentModel.DescriptionAttribute
            string description = "";
            var d = (DescriptionAttribute)prop.GetCustomAttribute(typeof(DescriptionAttribute), false);
            if (d != null)
            {
                description = d.Description;
            }

            LambdaExpression le = Expression.Lambda(prop.MemberType == MemberTypes.Property ? Expression.Property(param, prop.Name) : Expression.Field(param, prop.Name), param);
            var attributes = prop.GetCustomAttributes(typeof(GraphQLAuthorizeAttribute), true).Cast<GraphQLAuthorizeAttribute>();
            var requiredClaims = new RequiredClaims(attributes);
            var returnType = le.ReturnType.IsEnumerableOrArray() ? le.ReturnType.GetEnumerableOrArrayType() : le.ReturnType;
            var t = CacheType(returnType, schema, createEnumTypes, createNewComplexTypes);
            var f = new Field(SchemaGenerator.ToCamelCaseStartsLower(prop.Name), le, description, null, null, requiredClaims);
            if (t != null && t.IsEnum && !f.ReturnTypeClr.IsNullableType())
            {
                f.ReturnTypeNotNullable = true;
            }
            return f;
        }

        private static ISchemaType CacheType(Type propType, ISchemaProvider schema, bool createEnumTypes, bool createNewComplexTypes)
        {
            if (propType.IsEnumerableOrArray())
            {
                propType = propType.GetEnumerableOrArrayType();
            }

            if (!schema.HasType(propType) && !ignoreTypes.Contains(propType.Name))
            {
                var typeInfo = propType.GetTypeInfo();
                string description = "";
                var d = (DescriptionAttribute)typeInfo.GetCustomAttribute(typeof(DescriptionAttribute), false);
                if (d != null)
                {
                    description = d.Description;
                }

                if (createNewComplexTypes && (typeInfo.IsClass || typeInfo.IsInterface))
                {
                    // add type before we recurse more that may also add the type
                    // dynamcially call generic method
                    // hate this, but want to build the types with the right Genenics so you can extend them later.
                    // this is not the fastest, but only done on schema creation
                    var method = schema.GetType().GetMethod("AddType", new[] { typeof(string), typeof(string) });
                    method = method.MakeGenericMethod(propType);
                    var t = (ISchemaType)method.Invoke(schema, new object[] { propType.Name, description });

                    var fields = GetFieldsFromObject(propType, schema, createEnumTypes);
                    t.AddFields(fields);
                    return t;
                }
                else if (createEnumTypes && typeInfo.IsEnum && !schema.HasType(propType.Name))
                {
                    var t = schema.AddEnum(propType.Name, propType, description);
                    return t;
                }
                else if (createEnumTypes && propType.IsNullableType() && Nullable.GetUnderlyingType(propType).GetTypeInfo().IsEnum && !schema.HasType(Nullable.GetUnderlyingType(propType).Name))
                {
                    Type type = Nullable.GetUnderlyingType(propType);
                    var t = schema.AddEnum(type.Name, type, description);
                    return t;
                }
            }
            else if (schema.HasType(propType.Name))
            {
                return schema.Type(propType.Name);
            }
            return null;
        }
    }
}
