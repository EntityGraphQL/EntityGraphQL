using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Reflection;
using Humanizer;
using EntityGraphQL.Compiler.Util;
using System.ComponentModel;
using EntityGraphQL.Authorization;
using Microsoft.Extensions.Logging;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.FieldExtensions;
using System.Collections.ObjectModel;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// A simple schema provider to automattically create a query schema based on an object.
    /// Commonly used with a DbContext.
    /// </summary>
    public static class SchemaBuilder
    {
        private static readonly HashSet<string> ignoreProps = new()
        {
            "Database",
            "Model",
            "ChangeTracker",
            "ContextId"
        };

        private static readonly HashSet<string> ignoreTypes = new()
        {
            "String",
            "Byte[]"
        };
        public static readonly Func<string, string> DefaultNamer = name =>
        {
            return name[..1].ToLowerInvariant() + name[1..];
        };

        public static SchemaProvider<TContext> Create<TContext>(Func<string, string>? fieldNamer = null, ILogger<SchemaProvider<TContext>>? logger = null)
        {
            return Create(new RoleBasedAuthorization(), fieldNamer, logger);
        }
        public static SchemaProvider<TContext> Create<TContext>(IGqlAuthorizationService authorizationService, Func<string, string>? fieldNamer = null, ILogger<SchemaProvider<TContext>>? logger = null)
        {
            return new SchemaProvider<TContext>(authorizationService, fieldNamer, logger);
        }

        /// <summary>
        /// Given the type TContextType recursively create a query schema based on the public properties of the object.
        /// </summary>
        /// <param name="autoCreateIdArguments">If true (default), automatically create a field for any root array thats context object contains an Id property. I.e. If Actor has an Id property and the root TContextType contains IEnumerable<Actor> Actors. A root field Actor(id) will be created.</param>
        /// <param name="fieldNamer">Optionally provider a function to generate the GraphQL field name. By default this will make fields names that follow GQL style in lowerCaseCamelStyle</param>
        /// <typeparam name="TContextType"></typeparam>
        /// <returns></returns>
        public static SchemaProvider<TContextType> FromObject<TContextType>(bool autoCreateIdArguments = true, bool autoCreateEnumTypes = true, Func<string, string>? fieldNamer = null, bool introspectionEnabled = true)
        {
            return FromObject<TContextType>(new RoleBasedAuthorization(), autoCreateIdArguments, autoCreateEnumTypes, fieldNamer, introspectionEnabled);
        }

        public static SchemaProvider<TContextType> FromObject<TContextType>(IGqlAuthorizationService authorizationService, bool autoCreateIdArguments = true, bool autoCreateEnumTypes = true, Func<string, string>? fieldNamer = null, bool introspectionEnabled = true)
        {
            var schema = new SchemaProvider<TContextType>(authorizationService, fieldNamer ?? DefaultNamer, introspectionEnabled: introspectionEnabled);
            return FromObject(schema, autoCreateIdArguments, autoCreateEnumTypes, fieldNamer ?? DefaultNamer);
        }

        /// <summary>
        /// Given the type TContextType recursively create a query schema based on the public properties of the object. Schema is added into the provider schema
        /// </summary>
        /// <param name="schema">Schema tp add types to.</param>
        /// <param name="autoCreateIdArguments">If true (default), automatically create a field for any root array thats context object contains an Id property. I.e. If Actor has an Id property and the root TContextType contains IEnumerable<Actor> Actors. A root field Actor(id) will be created.</param>
        /// <param name="fieldNamer">Optionally provider a function to generate the GraphQL field name. By default this will make fields names that follow GQL style in lowerCaseCamelStyle</param>
        /// <typeparam name="TContextType"></typeparam>
        /// <returns></returns>
        public static SchemaProvider<TContextType> FromObject<TContextType>(SchemaProvider<TContextType> schema, bool autoCreateIdArguments = true, bool autoCreateEnumTypes = true, Func<string, string>? fieldNamer = null)
        {
            if (fieldNamer == null)
                fieldNamer = DefaultNamer;
            var contextType = typeof(TContextType);
            var rootFields = GetFieldsFromObject(contextType, schema, autoCreateEnumTypes, autoCreateIdArguments, fieldNamer);
            foreach (var f in rootFields)
            {             
                schema.Query().AddField(f);
            }
            return schema;
        }

        private static Field? AddFieldWithIdArgumentIfExists(ISchemaProvider schema, Type contextType, Field fieldProp, Func<string, string> fieldNamer)
        {
            if (fieldProp.ResolveExpression == null)
                throw new ArgumentException($"Field {fieldProp.Name} does not have a resolve function. This is required for autoCreateIdArguments to work.");
            if (!fieldProp.ResolveExpression.Type.IsEnumerableOrArray())
                return null;
            var schemaType = fieldProp.ReturnType.SchemaType;
            var idFieldDef = schemaType.GetFields().FirstOrDefault(f => f.Name == "id");
            if (idFieldDef == null)
                return null;

            if (idFieldDef.ResolveExpression == null)
                throw new ArgumentException($"Field {idFieldDef.Name} does not have a resolve function. This is required for autoCreateIdArguments to work.");

            // We need to build an anonymous type with id = RequiredField<idFieldDef.Resolve.Type>()
            // Resulting lambda is (a, p) => a.Where(b => b.Id == p.Id).First()
            // This allows us to "insert" .Select() (and .Include()) before the .First()
            var requiredFieldType = typeof(RequiredField<>).MakeGenericType(idFieldDef.ResolveExpression.Type);
            var fieldNameAndType = new Dictionary<string, Type> { { "id", requiredFieldType } };
            var argTypes = LinqRuntimeTypeBuilder.GetDynamicType(fieldNameAndType);
            var argTypesValue = Activator.CreateInstance(argTypes);
            var argTypeParam = Expression.Parameter(argTypes, $"args_{argTypes.Name}");
            Type arrayContextType = schemaType.TypeDotnet;
            var arrayContextParam = Expression.Parameter(arrayContextType, $"arrcxt_{arrayContextType.Name}");
            var ctxId = Expression.PropertyOrField(arrayContextParam, "Id");
            Expression argId = Expression.PropertyOrField(argTypeParam, "id");
            argId = Expression.Property(argId, "Value"); // call RequiredField<>.Value to get the real type without a convert
            var idBody = Expression.MakeBinary(ExpressionType.Equal, ctxId, argId);
            var idLambda = Expression.Lambda(idBody, new[] { arrayContextParam });
            Expression body = ExpressionUtil.MakeCallOnQueryable("Where", new Type[] { arrayContextType }, fieldProp.ResolveExpression, idLambda);

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
            return new Field(schema, name, selectionExpression, $"Return a {fieldProp.ReturnType.SchemaType.Name} by its Id", argTypesValue, new GqlTypeInfo(fieldProp.ReturnType.SchemaTypeGetter, selectionExpression.Body.Type), fieldProp.RequiredAuthorization);            
        }

        public static List<Field> GetFieldsFromObject(Type type, ISchemaProvider schema, bool createEnumTypes, bool autoCreateIdArguments, Func<string, string> fieldNamer, bool createNewComplexTypes = true, bool isInputType = false)
        {
            if (fieldNamer == null)
                fieldNamer = DefaultNamer;

            var fields = new List<Field>();
            // cache fields/properties
            var param = Expression.Parameter(type, $"p_{type.Name}");
            if (type.IsArray || type.IsEnumerableOrArray())
                return fields;

            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var f = ProcessFieldOrProperty(prop, param, schema, createEnumTypes, autoCreateIdArguments, createNewComplexTypes, fieldNamer, isInputType);
                if (f != null)
                    fields.AddRange(f);
            }
            foreach (var prop in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var f = ProcessFieldOrProperty(prop, param, schema, createEnumTypes, autoCreateIdArguments, createNewComplexTypes, fieldNamer, isInputType);
                if (f != null)
                    fields.AddRange(f);
            }
            return fields;
        }


        private static IEnumerable<Field>? ProcessFieldOrProperty(MemberInfo prop, ParameterExpression param, ISchemaProvider schema, bool createEnumTypes, bool autoCreateIdArguments, bool createNewComplexTypes, Func<string, string> fieldNamer, bool isInputType)
        {
            if (ignoreProps.Contains(prop.Name) || GraphQLIgnoreAttribute.ShouldIgnoreMemberFromQuery(prop))
                yield break;

            // Get Description from ComponentModel.DescriptionAttribute
            string description = string.Empty;
            var d = (DescriptionAttribute?)prop.GetCustomAttribute(typeof(DescriptionAttribute), false);
            if (d != null)
            {
                description = d.Description;
            }

            LambdaExpression le = Expression.Lambda(prop.MemberType == MemberTypes.Property ? Expression.Property(param, prop.Name) : Expression.Field(param, prop.Name), param);
            var attributes = prop.GetCustomAttributes(typeof(GraphQLAuthorizeAttribute), true).Cast<GraphQLAuthorizeAttribute>();
            var requiredClaims = schema.AuthorizationService.GetRequiredAuthFromMember(prop);
            // get the object type returned (ignoring list etc) so we know the context to find fields etc
            Type returnType;
            if (le.ReturnType.IsDictionary())
            {
                // check for dictionaries
                if (!createNewComplexTypes)
                    yield break;
                Type[] genericTypeArguments = le.ReturnType.GenericTypeArguments;
                returnType = typeof(KeyValuePair<,>).MakeGenericType(genericTypeArguments);
                if (!schema.HasType(returnType))
                    schema.AddScalarType(returnType, $"{genericTypeArguments[0].Name}{genericTypeArguments[1].Name}KeyValuePair", $"Key value pair of {genericTypeArguments[0].Name} & {genericTypeArguments[1].Name}");
            }
            else
                returnType = le.ReturnType.IsEnumerableOrArray() ? le.ReturnType.GetEnumerableOrArrayType()! : le.ReturnType.GetNonNullableType();

            var baseReturnType = returnType;
            if (baseReturnType.IsEnumerableOrArray())
                baseReturnType = baseReturnType.GetEnumerableOrArrayType()!;


            CacheType(baseReturnType, schema, createEnumTypes, autoCreateIdArguments, createNewComplexTypes, fieldNamer, isInputType);

            // see if there is a direct type mapping from the expression return to to something.
            // otherwise build the type info
            var returnTypeInfo = schema.GetCustomTypeMapping(le.ReturnType) ?? new GqlTypeInfo(() => schema.GetSchemaType(baseReturnType, null), le.Body.Type, prop);
            var field = new Field(schema, fieldNamer(prop.Name), le, description, returnTypeInfo, requiredClaims);

            if (autoCreateIdArguments)
            {
                // add non-pural field with argument of ID
                var idArgField = AddFieldWithIdArgumentIfExists(schema, prop.ReflectedType, field, fieldNamer);
                if(idArgField != null)
                {
                    yield return idArgField;
                }
            }

            var extensions = prop.GetCustomAttributes(typeof(FieldExtensionAttribute), false)?.Cast<FieldExtensionAttribute>().ToList();
            if (extensions?.Count > 0)
            {
                foreach (var extension in extensions)
                {
                    extension.ApplyExtension(field);
                }
            }

            yield return field;
        }

        private static void CacheType(Type propType, ISchemaProvider schema, bool createEnumTypes, bool autoCreateIdArguments, bool createNewComplexTypes, Func<string, string> fieldNamer, bool isInputType)
        {
            if (!schema.HasType(propType) && !ignoreTypes.Contains(propType.Name))
            {
                var typeInfo = propType;
                string description = string.Empty;
                var d = (DescriptionAttribute?)typeInfo.GetCustomAttribute(typeof(DescriptionAttribute), false);
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

                    var addMethod = isInputType ? "AddInputType" : (typeInfo.IsInterface || typeInfo.IsAbstract) ? "AddInterface" : "AddType";

                    var method = schema.GetType().GetMethod(addMethod, new[] { typeof(string), typeof(string) });
                    if (method == null)
                        throw new Exception($"Could not find {addMethod} method on schema");
                    method = method.MakeGenericMethod(propType);
                    var typeAdded = (ISchemaType)method.Invoke(schema, new object[] { propType.Name, description })!;                    
                    typeAdded.RequiredAuthorization = schema.AuthorizationService.GetRequiredAuthFromType(propType);

                    var fields = GetFieldsFromObject(propType, schema, createEnumTypes, autoCreateIdArguments, fieldNamer, createNewComplexTypes, isInputType);
                    typeAdded.AddFields(fields);
                }
                else if (createEnumTypes && typeInfo.IsEnum && !schema.HasType(propType.Name))
                {
                    schema.AddEnum(propType.Name, propType, description);
                }
                else if (createEnumTypes && propType.IsNullableType() && Nullable.GetUnderlyingType(propType)!.IsEnum && !schema.HasType(Nullable.GetUnderlyingType(propType)!.Name))
                {
                    Type type = Nullable.GetUnderlyingType(propType)!;
                    schema.AddEnum(type.Name, type, description);
                }
            }
        }

        public static GqlTypeInfo MakeGraphQlType(ISchemaProvider schema, Type returnType, string? returnSchemaType)
        {
            return new GqlTypeInfo(!string.IsNullOrEmpty(returnSchemaType) ? () => schema.Type(returnSchemaType) : () => schema.GetSchemaType(returnType.GetNonNullableOrEnumerableType(), null), returnType);
        }
    }
}
