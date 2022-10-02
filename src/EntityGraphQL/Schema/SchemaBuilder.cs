using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Reflection;
using Humanizer;
using EntityGraphQL.Compiler.Util;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using EntityGraphQL.Extensions;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// A simple schema provider to automatically create a query schema based on an object.
    /// Commonly used with a DbContext.
    /// </summary>
    public static class SchemaBuilder
    {
        /// <summary>
        /// Create a new SchemaProvider<TContext> with the query context of type TContext and using the SchemaBuilderSchemaOptions supplied or the default if null.
        /// Note the schema is empty, you need to add types and fields.
        /// </summary>
        /// <typeparam name="TContext">Query context type</typeparam>
        /// <param name="options">SchemaBuilderSchemaOptions to configure the options of the schema provider created</param>
        /// <param name="logger">A logger to use in the schema</param>
        /// <returns></returns>
        public static SchemaProvider<TContext> Create<TContext>(SchemaBuilderSchemaOptions? options = null, ILogger<SchemaProvider<TContext>>? logger = null)
        {
            if (options == null)
                options = new SchemaBuilderSchemaOptions();
            return new SchemaProvider<TContext>(options.AuthorizationService, options.FieldNamer, logger, options.IntrospectionEnabled);
        }

        /// <summary>
        /// Given the type TContextType recursively create a query schema based on the public properties of the object.
        /// </summary>
        /// <param name="buildOptions">SchemaBuilderOptions to use to create the SchemaProvider and configure the rules for auto creating the schema types and fields</param>
        /// <param name="logger">A logger to use in the schema</param>
        /// <typeparam name="TContextType"></typeparam>
        /// <returns></returns>
        public static SchemaProvider<TContextType> FromObject<TContextType>(SchemaBuilderOptions? buildOptions = null, ILogger<SchemaProvider<TContextType>>? logger = null)
        {
            if (buildOptions == null)
                buildOptions = new SchemaBuilderOptions();
            var schemaOptions = new SchemaBuilderSchemaOptions();

            var schema = new SchemaProvider<TContextType>(schemaOptions.AuthorizationService, schemaOptions.FieldNamer, logger, schemaOptions.IntrospectionEnabled);
            return FromObject(schema, buildOptions);
        }

        /// <summary>
        /// Given the type TContextType recursively create a query schema based on the public properties of the object.
        /// </summary>
        /// <param name="schemaOptions">Options to create the SchemaProvider.</param>
        /// <param name="buildOptions">SchemaBuilderOptions to use to create the SchemaProvider and configure the rules for auto creating the schema types and fields</param>
        /// <param name="logger">A logger to use in the schema</param>
        /// <typeparam name="TContextType"></typeparam>
        /// <returns></returns>
        public static SchemaProvider<TContextType> FromObject<TContextType>(SchemaBuilderSchemaOptions? schemaOptions, SchemaBuilderOptions? buildOptions = null, ILogger<SchemaProvider<TContextType>>? logger = null)
        {
            if (buildOptions == null)
                buildOptions = new SchemaBuilderOptions();
            if (schemaOptions == null)
                schemaOptions = new SchemaBuilderSchemaOptions();

            var schema = new SchemaProvider<TContextType>(schemaOptions.AuthorizationService, schemaOptions.FieldNamer, logger, schemaOptions.IntrospectionEnabled);
            schemaOptions.PreBuildSchemaFromContext?.Invoke(schema);
            return FromObject(schema, buildOptions);
        }

        /// <summary>
        /// Given the type TContextType recursively create a query schema based on the public properties of the object. Schema is added into the provider schema
        /// </summary>
        /// <param name="schema">Schema to add types to.</param>
        /// <param name="options">SchemaBuilderOptions to use to create the SchemaProvider and configure the rules for auto creating the schema types and fields</param>
        /// <typeparam name="TContextType"></typeparam>
        /// <returns></returns>
        internal static SchemaProvider<TContextType> FromObject<TContextType>(SchemaProvider<TContextType> schema, SchemaBuilderOptions options)
        {
            var contextType = typeof(TContextType);
            var rootFields = GetFieldsFromObject(contextType, schema.Query(), schema, options, false);
            foreach (var f in rootFields)
            {
                schema.Query().AddField(f);
            }
            return schema;
        }

        private static Field? AddFieldWithIdArgumentIfExists(ISchemaProvider schema, ISchemaType schemaType, Type contextType, Field fieldProp)
        {
            if (fieldProp.ResolveExpression == null)
                throw new ArgumentException($"Field {fieldProp.Name} does not have a resolve function. This is required for AutoCreateIdArguments to work.");
            if (!fieldProp.ResolveExpression.Type.IsEnumerableOrArray())
                return null;
            var returnSchemaType = fieldProp.ReturnType.SchemaType;
            var idFieldDef = returnSchemaType.GetFields().FirstOrDefault(f => f.Name == "id");
            if (idFieldDef == null)
                return null;

            if (idFieldDef.ResolveExpression == null)
                throw new ArgumentException($"Field {idFieldDef.Name} does not have a resolve function. This is required for AutoCreateIdArguments to work.");

            // We need to build an anonymous type with id = RequiredField<idFieldDef.Resolve.Type>()
            // Resulting lambda is (a, p) => a.Where(b => b.Id == p.Id).First()
            // This allows us to "insert" .Select() (and .Include()) before the .First()
            var requiredFieldType = typeof(RequiredField<>).MakeGenericType(idFieldDef.ResolveExpression.Type);
            var fieldNameAndType = new Dictionary<string, Type> { { "id", requiredFieldType } };
            var argTypes = LinqRuntimeTypeBuilder.GetDynamicType(fieldNameAndType, fieldProp.Name);
            var argTypesValue = Activator.CreateInstance(argTypes);
            var argTypeParam = Expression.Parameter(argTypes, $"args_{argTypes.Name}");
            Type arrayContextType = returnSchemaType.TypeDotnet;
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
            body = new ParameterReplacer().Replace(body, fieldProp.FieldParam!, contextParam);
            var selectionExpression = Expression.Lambda(body, lambdaParams);
            var name = fieldProp.Name.Singularize();
            if (name == null || name == fieldProp.Name)
            {
                // If we can't singularize it (or it returns the same name) just use the name plus something as GraphQL doesn't support field overloads
                name = $"{fieldProp.Name}ById";
            }
            return new Field(schema, schemaType, name, selectionExpression, $"Return a {fieldProp.ReturnType.SchemaType.Name} by its Id", argTypesValue, new GqlTypeInfo(fieldProp.ReturnType.SchemaTypeGetter, selectionExpression.Body.Type), fieldProp.RequiredAuthorization);
        }

        public static List<Field> GetFieldsFromObject(Type type, ISchemaType fromType, ISchemaProvider schema, SchemaBuilderOptions options, bool isInputType)
        {
            var fields = new List<Field>();
            // cache fields/properties
            var param = Expression.Parameter(type, $"p_{type.Name}");
            if (type.IsArray || type.IsEnumerableOrArray())
                return fields;

            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var f = ProcessFieldOrProperty(fromType, prop, param, schema, options, isInputType)?.ToList();
                if (f != null)
                    fields.AddRange(f);
            }
            foreach (var prop in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var f = ProcessFieldOrProperty(fromType, prop, param, schema, options, isInputType)?.ToList();
                if (f != null)
                    fields.AddRange(f);
            }
            return fields;
        }


        private static IEnumerable<Field>? ProcessFieldOrProperty(ISchemaType fromType, MemberInfo prop, ParameterExpression param, ISchemaProvider schema, SchemaBuilderOptions options, bool isInputType)
        {
            if (options.IgnoreProps.Contains(prop.Name) || GraphQLIgnoreAttribute.ShouldIgnoreMemberFromQuery(prop))
                yield break;

            if (isInputType && GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(prop))
                yield break;

            // Get Description from ComponentModel.DescriptionAttribute
            string description = string.Empty;
            var d = (DescriptionAttribute?)prop.GetCustomAttribute(typeof(DescriptionAttribute), false);
            if (d != null)
            {
                description = d.Description;
            }

            LambdaExpression le = Expression.Lambda(prop.MemberType == MemberTypes.Property ? Expression.Property(param, prop.Name) : Expression.Field(param, prop.Name), param);

            var requiredClaims = schema.AuthorizationService.GetRequiredAuthFromMember(prop);
            // get the object type returned (ignoring list etc) so we know the context to find fields etc
            var returnsTask = le.ReturnType.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) != null || (le.ReturnType.IsGenericType && le.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
            Type returnType = le.ReturnType;
            if (returnsTask || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)))
            {
                returnType = returnType.GetGenericArguments()[0];
            }
            if (returnType.IsDictionary())
            {
                // check for dictionaries
                if (!options.AutoCreateNewComplexTypes)
                    yield break;
                Type[] genericTypeArguments = returnType.GenericTypeArguments;
                returnType = typeof(KeyValuePair<,>).MakeGenericType(genericTypeArguments);
                if (!schema.HasType(returnType))
                    schema.AddScalarType(returnType, $"{genericTypeArguments[0].Name}{genericTypeArguments[1].Name}KeyValuePair", $"Key value pair of {genericTypeArguments[0].Name} & {genericTypeArguments[1].Name}");
            }
            else
                returnType = returnType.IsEnumerableOrArray() ? returnType.GetEnumerableOrArrayType()! : returnType.GetNonNullableType();

            var baseReturnType = returnType;
            if (baseReturnType.IsEnumerableOrArray())
                baseReturnType = baseReturnType.GetEnumerableOrArrayType()!;


            if (!options.IgnoreTypes.Contains(baseReturnType!))
            {
                CacheType(baseReturnType, schema, options, isInputType);

                // see if there is a direct type mapping from the expression return to to something.
                // otherwise build the type info
                var returnTypeInfo = schema.GetCustomTypeMapping(le.ReturnType) ?? new GqlTypeInfo(() => schema.GetSchemaType(baseReturnType, null), le.Body.Type, prop.IsNullable());
                var field = new Field(schema, fromType, schema.SchemaFieldNamer(prop.Name), le, description, null, returnTypeInfo, requiredClaims);

                if (options.AutoCreateFieldWithIdArguments && (!schema.HasType(prop.DeclaringType!) || schema.GetSchemaType(prop.DeclaringType!, null).GqlType != GqlTypeEnum.Input))
                {
                    // add non-pural field with argument of ID
                    var idArgField = AddFieldWithIdArgumentIfExists(schema, fromType, prop.ReflectedType!, field);
                    if (idArgField != null)
                    {
                        yield return idArgField;
                    }
                }

                field.ApplyAttributes(prop.GetCustomAttributes());

                yield return field;
            }
        }

        internal static ISchemaType CacheType(Type propType, ISchemaProvider schema, SchemaBuilderOptions options, bool isInputType)
        {
            if (!schema.HasType(propType))
            {
                var typeInfo = propType;
                string description = string.Empty;
                var d = (DescriptionAttribute?)typeInfo.GetCustomAttribute(typeof(DescriptionAttribute), false);
                if (d != null)
                {
                    description = d.Description;
                }

                var typeName = BuildTypeName(propType);

                if ((options.AutoCreateNewComplexTypes && typeInfo.IsClass) || ((typeInfo.IsInterface || typeInfo.IsAbstract) && options.AutoCreateInterfaceTypes))
                {
                    var fieldCount = typeInfo.GetProperties().Length + typeInfo.GetFields().Length;

                    // add type before we recurse more that may also add the type
                    // dynamcially call generic method
                    // hate this, but want to build the types with the right Genenics so you can extend them later.
                    // this is not the fastest, but only done on schema creation
                    var addMethod = (isInputType, typeInfo.IsInterface, typeInfo.IsAbstract, fieldCount) switch
                    {
                        (true, _, _, _) => nameof(ISchemaProvider.AddInputType),
                        (_, true, _, > 0) => nameof(ISchemaProvider.AddInterface),
                        (_, _, true, > 0) => nameof(ISchemaProvider.AddInterface),
                        (_, true, _, _) => nameof(ISchemaProvider.AddUnion),
                        (_, _, true, _) => nameof(ISchemaProvider.AddUnion),
                        _ => nameof(ISchemaProvider.AddType)
                    };

                    var method = schema.GetType().GetMethod(addMethod, new[] { typeof(string), typeof(string) });
                    if (method == null)
                        throw new Exception($"Could not find {addMethod} method on schema");
                    method = method.MakeGenericMethod(propType);
                    var typeAdded = (ISchemaType)method.Invoke(schema, new object[] { typeName, description })!;
                    typeAdded.RequiredAuthorization = schema.AuthorizationService.GetRequiredAuthFromType(propType);

                    var fields = GetFieldsFromObject(propType, typeAdded, schema, options, isInputType);
                    typeAdded.AddFields(fields);


                    if (options.AutoCreateInterfaceTypes)
                    {
                        typeAdded.ImplementAllBaseTypes(true, true);
                    }
                    return typeAdded;
                }
                else if (options.AutoCreateEnumTypes && typeInfo.IsEnum && !schema.HasType(typeName))
                {
                    return schema.AddEnum(propType.Name, propType, description);
                }
                else if (options.AutoCreateEnumTypes && propType.IsNullableType() && Nullable.GetUnderlyingType(propType)!.IsEnum && !schema.HasType(Nullable.GetUnderlyingType(propType)!.Name))
                {
                    Type type = Nullable.GetUnderlyingType(propType)!;
                    return schema.AddEnum(type.Name, type, description);
                }
                else
                {
                    var type = schema.GetSchemaType(propType, null);
                    if (options.AutoCreateInterfaceTypes)
                    {
                        type.ImplementAllBaseTypes(true, true);
                    }

                    var schemaType = schema.GetSchemaType(propType, null);
                    schemaType.ApplyAttributes(propType.GetCustomAttributes());

                    return type;
                }
            }
            else
                return schema.GetSchemaType(propType, null);
        }

        internal static string BuildTypeName(Type propType)
        {
            return propType.IsGenericType ? $"{propType.Name[..propType.Name.IndexOf('`')]}{string.Join("", propType.GetGenericArguments().Select(BuildTypeName))}" : propType.Name;
        }

        public static GqlTypeInfo MakeGraphQlType(ISchemaProvider schema, Type returnType, string? returnSchemaType)
        {
            return new GqlTypeInfo(!string.IsNullOrEmpty(returnSchemaType) ? () => schema.Type(returnSchemaType) : () => schema.GetSchemaType(returnType.GetNonNullableOrEnumerableType(), null), returnType);
        }
    }
}
