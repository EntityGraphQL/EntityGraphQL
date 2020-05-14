using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using EntityGraphQL.Authorization;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Directives;
using EntityGraphQL.Extensions;
using EntityGraphQL.LinqQuery;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Builder interface to build a schema definition. The built schema definition maps an external view of your data model to you internal model.
    /// This allows your internal model to change over time while not break your external API. You can create new versions when needed.
    /// </summary>
    /// <typeparam name="TContextType">Base object graph. Ex. DbContext</typeparam>
    public class SchemaProvider<TContextType> : ISchemaProvider
    {
        protected Dictionary<string, ISchemaType> types = new Dictionary<string, ISchemaType>();
        protected Dictionary<string, MutationType> mutations = new Dictionary<string, MutationType>();
        protected Dictionary<string, IDirectiveProcessor> directives = new Dictionary<string, IDirectiveProcessor>
        {
            // the 2 inbuilt directives defined by gql
            {"include", new IncludeDirectiveProcessor()},
            {"skip", new SkipDirectiveProcessor()},
        };

        private readonly string queryContextName;
        // we want this scalar string values to be unique
        private readonly Dictionary<Type, string> customScalarTypes = new Dictionary<Type, string> {
            {typeof(int), "Int"},
            {typeof(float), "Float"},
            {typeof(string), "String"},
            {typeof(bool), "Boolean"},
            {typeof(Guid), "ID"},
        };
        // map some types to scalar types
        protected Dictionary<Type, string> customTypeMappings = new Dictionary<Type, string> {
            {typeof(uint), "Int!"},
            {typeof(ulong), "Int!"},
            {typeof(long), "Int!"},
            {typeof(double), "Float!"},
            {typeof(decimal), "Float!"},
            {typeof(byte[]), "String"},
        };
        public IEnumerable<string> CustomScalarTypes { get { return customScalarTypes.Values; } }

        public SchemaProvider()
        {
            var queryContext = new SchemaType<TContextType>(this, typeof(TContextType).Name, "Query schema");
            queryContextName = queryContext.Name;
            types.Add(queryContext.Name, queryContext);

            AddType<Models.TypeElement>("__Type", "Information about types").AddAllFields();
            AddType<Models.EnumValue>("__EnumValue", "Information about enums").AddAllFields();
            AddType<Models.InputValue>("__InputValue", "Arguments provided to Fields or Directives and the input fields of an InputObject are represented as Input Values which describe their type and optionally a default value.").AddAllFields();
            AddType<Models.Directives>("__Directive", "Information about directives").AddAllFields();
            AddType<Models.Field>("__Field", "Information about fields").AddAllFields();
            AddType<Models.SubscriptionType>("Information about subscriptions").AddAllFields();
            AddType<Models.Schema>("__Schema", "A GraphQL Schema defines the capabilities of a GraphQL server. It exposes all available types and directives on the server, as well as the entry points for query, mutation, and subscription operations.").AddAllFields();

            Type<Models.TypeElement>("__Type").ReplaceField("enumValues", new { includeDeprecated = false },
                (t, p) => t.EnumValues.Where(f => p.includeDeprecated ? f.IsDeprecated || !f.IsDeprecated : !f.IsDeprecated).ToList(), "Enum values available on type");

            SetupIntrospectionTypesAndField();
        }

        /// <summary>
        /// Execute a query using this schema.
        /// </summary>
        /// <param name="gql">The query</param>
        /// <param name="context">The context object. An instance of the context the schema was build from</param>
        /// <param name="serviceProvider">A service provider used for looking up dependencies of field selections and mutations</param>
        /// <param name="claims">Optional claims to check access for queries</param>
        /// <param name="methodProvider"></param>
        /// <param name="includeDebugInfo"></param>
        /// <typeparam name="TContextType"></typeparam>
        /// <returns></returns>
        public QueryResult ExecuteQuery(QueryRequest gql, TContextType context, IServiceProvider serviceProvider, ClaimsIdentity claims, IMethodProvider methodProvider = null)
        {
            if (methodProvider == null)
                methodProvider = new DefaultMethodProvider();

            QueryResult result;
            try
            {
                var graphQLCompiler = new GraphQLCompiler(this, methodProvider);
                var queryResult = graphQLCompiler.Compile(gql, claims);
                result = queryResult.ExecuteQuery(context, serviceProvider, gql.OperationName);
            }
            catch (Exception ex)
            {
                // error with the whole query
                result = new QueryResult { Errors = { new GraphQLError(ex.InnerException != null ? ex.InnerException.Message : ex.Message) } };
            }

            return result;
        }

        private void SetupIntrospectionTypesAndField()
        {
            var allTypeMappings = SchemaGenerator.DefaultTypeMappings.ToDictionary(k => k.Key, v => v.Value);
            // add the top level __schema field which is made _at runtime_ currently e.g. introspection could be faster
            foreach (var item in customTypeMappings)
            {
                allTypeMappings[item.Key] = item.Value;
            }
            var combinedMapping = new CombinedMapping(allTypeMappings, customScalarTypes);

            // evaluate Fields lazily so we don't end up in endless loop
            Type<Models.TypeElement>("__Type").ReplaceField("fields", new { includeDeprecated = false },
                (t, p) => SchemaIntrospection.BuildFieldsForType(this, combinedMapping, t.Name).Where(f => p.includeDeprecated ? f.IsDeprecated || !f.IsDeprecated : !f.IsDeprecated).ToList(), "Fields available on type");


            ReplaceField("__schema", db => SchemaIntrospection.Make(this, combinedMapping), "Introspection of the schema", "__Schema");
            ReplaceField("__type", new { name = ArgumentHelper.Required<string>() }, (db, p) => SchemaIntrospection.Make(this, combinedMapping).Types.Where(s => s.Name == p.name).First(), "Query a type by name", "__Type");
        }

        /// <summary>
        /// Add a new type into the schema with TBaseType as it's context
        /// </summary>
        /// <param name="name">Name of the type</param>
        /// <param name="description">description of the type</param>
        /// <typeparam name="TBaseType"></typeparam>
        /// <returns></returns>
        public SchemaType<TBaseType> AddType<TBaseType>(string name, string description)
        {
            var tt = new SchemaType<TBaseType>(this, name, description);
            types.Add(name, tt);
            return tt;
        }

        public ISchemaType AddType(Type contextType, string name, string description)
        {
            var tt = new SchemaType<object>(this, contextType, name, description);
            types.Add(name, tt);
            return tt;
        }

        public SchemaType<TBaseType> AddInputType<TBaseType>(string name, string description)
        {
            var tt = new SchemaType<TBaseType>(this, name, description, true);
            types.Add(name, tt);
            return tt;
        }

        /// <summary>
        /// Add any methods marked with GraphQLMutationAttribute in the given type to the schema. Names are added as lowerCaseCamel`
        /// </summary>
        /// <param name="mutationClassInstance"></param>
        /// <typeparam name="TType"></typeparam>
        public void AddMutationFrom<TType>(TType mutationClassInstance)
        {
            foreach (var method in mutationClassInstance.GetType().GetMethods())
            {
                if (method.GetCustomAttribute(typeof(GraphQLMutationAttribute)) is GraphQLMutationAttribute attribute)
                {
                    string name = SchemaGenerator.ToCamelCaseStartsLower(method.Name);
                    var claims = method.GetCustomAttributes(typeof(GraphQLAuthorizeAttribute)).Cast<GraphQLAuthorizeAttribute>();
                    var requiredClaims = new RequiredClaims(claims);
                    var typeName = GetSchemaTypeNameForClrType(method.ReturnType);
                    var mutationType = new MutationType(name, types.ContainsKey(typeName) ? types[typeName] : null, mutationClassInstance, method, attribute.Description, requiredClaims);
                    mutations[name] = mutationType;
                }
            }
        }

        public bool HasMutation(string method)
        {
            return mutations.ContainsKey(method);
        }

        public void AddTypeMapping<TFrom>(string gqlType)
        {
            // add mapping
            customTypeMappings.Add(typeof(TFrom), gqlType);
            // add scalar if needed
            if (!HasType(gqlType) && !gqlType.StartsWith("["))
            {
                AddCustomScalarType(typeof(TFrom), gqlType);
            }
            SetupIntrospectionTypesAndField();
        }

        /// <summary>
        /// Adds a new type into the schema. The name defaults to the TBaseType name
        /// </summary>
        /// <param name="description"></param>
        /// <typeparam name="TBaseType"></typeparam>
        /// <returns></returns>
        public SchemaType<TBaseType> AddType<TBaseType>(string description)
        {
            var name = typeof(TBaseType).Name;
            return AddType<TBaseType>(name, description);
        }

        /// <summary>
        /// Add a field to the root type. This is where you define top level objects/names that you can query.
        /// The name defaults to the MemberExpression from selection modified to lowerCamelCase
        /// </summary>
        /// <param name="selection"></param>
        /// <param name="description"></param>
        /// <param name="returnSchemaType"></param>
        public Field AddField(Expression<Func<TContextType, object>> selection, string description, string returnSchemaType = null, bool? isNullable = null)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(selection);
            return AddField(SchemaGenerator.ToCamelCaseStartsLower(exp.Member.Name), selection, description, returnSchemaType, isNullable);
        }

        /// <summary>
        /// Add a field to the root type. This is where you define top level objects/names that you can query.
        /// Note the name you use is case sensistive. We recommend following GraphQL and useCamelCase as this library will for methods that use Expressions.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selection"></param>
        /// <param name="description"></param>
        /// <param name="returnSchemaType"></param>
        public Field AddField(string name, Expression<Func<TContextType, object>> selection, string description, string returnSchemaType = null, bool? isNullable = null)
        {
            return Type<TContextType>().AddField(name, selection, description, returnSchemaType, isNullable);
        }

        public Field ReplaceField<TParams, TReturn>(Expression<Func<TContextType, object>> selection, TParams argTypes, Expression<Func<TContextType, TParams, TReturn>> selectionExpression, string description, string returnSchemaType = null, bool? isNullable = null)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(selection);
            var name = SchemaGenerator.ToCamelCaseStartsLower(exp.Member.Name);
            Type<TContextType>().RemoveField(name);
            return Type<TContextType>().AddField(name, argTypes, selectionExpression, description, returnSchemaType, isNullable);
        }

        public Field ReplaceField<TReturn>(string name, Expression<Func<TContextType, TReturn>> selectionExpression, string description, string returnSchemaType = null, bool? isNullable = null)
        {
            Type<TContextType>().RemoveField(name);
            return Type<TContextType>().AddField(name, selectionExpression, description, returnSchemaType, isNullable);
        }

        public Field ReplaceField<TParams, TReturn>(string name, TParams argTypes, Expression<Func<TContextType, TParams, TReturn>> selectionExpression, string description, string returnSchemaType = null, bool? isNullable = null)
        {
            Type<TContextType>().RemoveField(name);
            return Type<TContextType>().AddField(name, argTypes, selectionExpression, description, returnSchemaType, isNullable);
        }

        /// <summary>
        /// Add a field with arguments.
        /// {
        ///     field(arg: val) {}
        /// }
        /// Note the name you use is case sensistive. We recommend following GraphQL and useCamelCase as this library will for methods that use Expressions.
        /// </summary>
        /// <param name="name">Field name</param>
        /// <param name="argTypes">Anonymous object defines the names and types of each argument</param>
        /// <param name="selectionExpression">The expression that selects the data from TContextType using the arguments</param>
        /// <param name="returnSchemaType">The schema type to return, it defines the fields available on the return object. If null, defaults to TReturn type mapped in the schema.</param>
        /// <typeparam name="TParams">Type describing the arguments</typeparam>
        /// <typeparam name="TReturn">The return entity type that is mapped to a type in the schema</typeparam>
        /// <returns></returns>
        public Field AddField<TParams, TReturn>(string name, TParams argTypes, Expression<Func<TContextType, TParams, TReturn>> selectionExpression, string description, string returnSchemaType = null, bool? isNullable = null)
        {
            return Type<TContextType>().AddField(name, argTypes, selectionExpression, description, returnSchemaType, isNullable);
        }
        /// <summary>
        /// Add a field to the root query.
        /// Note the name you use is case sensistive. We recommend following GraphQL and useCamelCase as this library will for methods that use Expressions.
        /// </summary>
        /// <param name="field"></param>
        public Field AddField(Field field)
        {
            return types[queryContextName].AddField(field);
        }

        /// <summary>
        /// Get registered type by TType name
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <returns></returns>
        public SchemaType<TType> Type<TType>()
        {
            // look up by the actual type not the name
            return (SchemaType<TType>)types.Values.Where(t => t.ContextType == typeof(TType)).First();
        }
        public SchemaType<TType> Type<TType>(string typeName)
        {
            return (SchemaType<TType>)types[typeName];
        }
        public ISchemaType Type(string typeName)
        {
            return types[typeName];
        }
        // ISchemaProvider interface
        public Type ContextType { get { return types[queryContextName].ContextType; } }
        public bool TypeHasField(string typeName, string identifier, IEnumerable<string> fieldArgs, ClaimsIdentity claims)
        {
            if (!types.ContainsKey(typeName))
                return false;
            var t = types[typeName];
            if (!t.HasField(identifier))
            {
                if ((fieldArgs == null || !fieldArgs.Any()) && t.HasField(identifier))
                {
                    var field = t.GetField(identifier, claims);
                    if (field != null)
                    {
                        // if there are defaults for all, continue
                        if (field.RequiredArgumentNames.Any())
                        {
                            throw new EntityGraphQLCompilerException($"Field '{identifier}' missing required argument(s) '{string.Join(", ", field.RequiredArgumentNames)}'");
                        }
                        return true;
                    }
                    else
                    {
                        throw new EntityGraphQLCompilerException($"Field '{identifier}' not found on current context '{typeName}'");
                    }
                }
                return false;
            }
            return true;
        }

        public bool TypeHasField(Type type, string identifier, IEnumerable<string> fieldArgs, ClaimsIdentity claims)
        {
            return TypeHasField(type.Name, identifier, fieldArgs, claims);
        }

        public List<ISchemaType> EnumTypes()
        {
            return types.Values.Where(t => t.IsEnum).ToList();
        }

        public string GetActualFieldName(string typeName, string identifier, ClaimsIdentity claims)
        {
            if (types.ContainsKey(typeName) && types[typeName].HasField(identifier))
                return types[typeName].GetField(identifier, claims).Name;

            if (typeName == queryContextName && types[queryContextName].HasField(identifier))
                return types[queryContextName].GetField(identifier, claims).Name;

            if (mutations.Keys.Any(k => k.ToLower() == identifier.ToLower()))
                return mutations.Keys.First(k => k.ToLower() == identifier.ToLower());

            throw new EntityGraphQLCompilerException($"Field {identifier} not found on type {typeName}");
        }

        public IMethodType GetFieldOnContext(Expression context, string fieldName, ClaimsIdentity claims)
        {
            if (context.Type == ContextType && mutations.ContainsKey(fieldName))
            {
                var mutation = mutations[fieldName];
                if (!AuthUtil.IsAuthorized(claims, mutation.AuthorizeClaims))
                {
                    throw new EntityGraphQLAccessException($"You do not have access to mutation '{fieldName}'. You require any of the following security claims [{string.Join(", ", mutation.AuthorizeClaims.Claims.SelectMany(r => r))}]");
                }
                return mutation;
            }
            if (types.ContainsKey(GetSchemaTypeNameForClrType(context.Type)))
            {
                var field = types[GetSchemaTypeNameForClrType(context.Type)].GetField(fieldName, claims);
                return field;
            }
            throw new EntityGraphQLCompilerException($"No field or mutation '{fieldName}' found in schema.");
        }

        public ExpressionResult GetExpressionForField(Expression context, string typeName, string fieldName, Dictionary<string, ExpressionResult> args, ClaimsIdentity claims)
        {
            if (!types.ContainsKey(typeName))
                throw new EntityQuerySchemaException($"{typeName} not found in schema.");

            var field = types[typeName].GetField(fieldName, claims);
            var result = new ExpressionResult(field.Resolve ?? Expression.Property(context, fieldName), field.Services);

            if (field.ArgumentTypesObject != null)
            {
                var argType = field.ArgumentTypesObject.GetType();
                // get the values for the argument anonymous type object constructor
                var propVals = new Dictionary<PropertyInfo, object>();
                var fieldVals = new Dictionary<FieldInfo, object>();
                // if they used AddField("field", new { id = Required<int>() }) the compiler makes properties and a constructor with the values passed in
                foreach (var argField in argType.GetProperties())
                {
                    var val = BuildArgumentFromMember(args, field, argField.Name, argField.PropertyType, argField.GetValue(field.ArgumentTypesObject));
                    // if this was a EntityQueryType we actually get a Func from BuildArgumentFromMember but the anonymous type requires EntityQueryType<>. We marry them here, this allows users to EntityQueryType<> as a Func in LINQ methods while not having it defined until runtime
                    if (argField.PropertyType.IsConstructedGenericType && argField.PropertyType.GetGenericTypeDefinition() == typeof(EntityQueryType<>))
                    {
                        // make sure we create a new instance and not update the schema
                        var entityQuery = Activator.CreateInstance(argField.PropertyType);

                        // set Query
                        var hasValue = val != null;
                        if (hasValue)
                        {
                            var genericProp = entityQuery.GetType().GetProperty("Query");
                            genericProp.SetValue(entityQuery, ((ExpressionResult)val).Expression);
                        }

                        propVals.Add(argField, entityQuery);
                    }
                    else
                    {
                        if (val != null && val.GetType() != argField.PropertyType)
                            val = ExpressionUtil.ChangeType(val, argField.PropertyType);
                        propVals.Add(argField, val);
                    }
                }
                // The auto argument is built at runtime from LinqRuntimeTypeBuilder which just makes public fields
                // they could also use a custom class, so we need to look for both fields and properties
                foreach (var argField in argType.GetFields())
                {
                    var val = BuildArgumentFromMember(args, field, argField.Name, argField.FieldType, argField.GetValue(field.ArgumentTypesObject));
                    fieldVals.Add(argField, val);
                }

                // create a copy of the anonymous object. It will have the default values set
                // there is only 1 constructor for the anonymous type that takes all the property values
                var con = argType.GetConstructor(propVals.Keys.Select(v => v.PropertyType).ToArray());
                object parameters;
                if (con != null)
                {
                    parameters = con.Invoke(propVals.Values.ToArray());
                    foreach (var item in fieldVals)
                    {
                        item.Key.SetValue(parameters, item.Value);
                    }
                }
                else
                {
                    // expect an empty constructor
                    con = argType.GetConstructor(new Type[0]);
                    parameters = con.Invoke(new object[0]);
                    foreach (var item in fieldVals)
                    {
                        item.Key.SetValue(parameters, item.Value);
                    }
                    foreach (var item in propVals)
                    {
                        item.Key.SetValue(parameters, item.Value);
                    }
                }
                // tell them this expression has another parameter
                var argParam = Expression.Parameter(argType, $"arg_{argType.Name}");
                result.Expression = new ParameterReplacer().ReplaceByType(result.Expression, argType, argParam);
                result.AddConstantParameter(argParam, parameters);
            }

            // the expressions we collect have a different starting parameter. We need to change that
            var paramExp = field.FieldParam;
            result.Expression = new ParameterReplacer().Replace(result.Expression, paramExp, context);

            return result;
        }

        private static object BuildArgumentFromMember(Dictionary<string, ExpressionResult> args, Field field, string memberName, Type memberType, object defaultValue)
        {
            string argName = memberName;
            // check we have required arguments
            if (memberType.GetGenericArguments().Any() && memberType.GetGenericTypeDefinition() == typeof(RequiredField<>))
            {
                if (args == null || !args.ContainsKey(argName))
                {
                    throw new EntityGraphQLCompilerException($"Field '{field.Name}' missing required argument '{argName}'");
                }
                var item = Expression.Lambda(args[argName]).Compile().DynamicInvoke();
                var constructor = memberType.GetConstructor(new[] { item.GetType() });
                if (constructor == null)
                {
                    // we might need to change the type
                    foreach (var c in memberType.GetConstructors())
                    {
                        var parameters = c.GetParameters();
                        if (parameters.Count() == 1)
                        {
                            item = ExpressionUtil.ChangeType(item, parameters[0].ParameterType);
                            constructor = memberType.GetConstructor(new[] { item.GetType() });
                            break;
                        }
                    }
                }

                if (constructor == null)
                {
                    throw new EntityGraphQLCompilerException($"Could not find a constructor for type {memberType.Name} that takes value '{item}'");
                }

                var typedVal = constructor.Invoke(new[] { item });
                return typedVal;
            }
            else if (defaultValue != null && defaultValue.GetType().IsConstructedGenericType && defaultValue.GetType().GetGenericTypeDefinition() == typeof(EntityQueryType<>))
            {
                return args != null && args.ContainsKey(argName) ? args[argName] : null;
            }
            else if (args != null && args.ContainsKey(argName))
            {
                return Expression.Lambda(args[argName]).Compile().DynamicInvoke();
            }
            else
            {
                // set the default value
                return defaultValue;
            }
        }

        public string GetSchemaTypeNameForClrType(Type type)
        {
            if (type.GetTypeInfo().BaseType == typeof(LambdaExpression))
            {
                // This should be Expression<Func<Context, ReturnType>>
                type = type.GetGenericArguments()[0].GetGenericArguments()[1];
                if (type.IsEnumerableOrArray())
                {
                    type = type.GetGenericArguments()[0];
                }
            }
            if (customScalarTypes.ContainsKey(type))
                return customScalarTypes[type];

            if (customTypeMappings.ContainsKey(type))
                return customTypeMappings[type];

            if (type == types[queryContextName].ContextType)
                return type.Name;

            foreach (var eType in types.Values)
            {
                if (eType.ContextType == type)
                    return eType.Name;
            }
            throw new EntityGraphQLCompilerException($"No mapped entity found for type '{type}'");
        }

        public bool HasType(string typeName)
        {
            return types.ContainsKey(typeName);
        }

        public bool HasType(Type type)
        {
            if (type == types[queryContextName].ContextType)
                return true;

            foreach (var eType in types.Values)
            {
                if (eType.ContextType == type)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Builds a GraphQL schema file
        /// </summary>
        /// <returns></returns>
        public string GetGraphQLSchema()
        {
            var extraMappings = customTypeMappings.ToDictionary(k => k.Key, v => v.Value);
            return SchemaGenerator.Make(this, extraMappings, this.customScalarTypes);
        }

        public void AddCustomScalarType(Type clrType, string gqlTypeName, bool required = false)
        {
            this.customScalarTypes[clrType] = gqlTypeName;
            this.customTypeMappings[clrType] = required ? gqlTypeName + "!" : gqlTypeName;
            // _customScalarMappings has change, need to make the introspectino again. Do this like this so we don't need to build the mappings inline
            SetupIntrospectionTypesAndField();
        }

        public IEnumerable<Field> GetQueryFields()
        {
            return types[queryContextName].GetFields();
        }

        public IEnumerable<ISchemaType> GetNonContextTypes()
        {
            return types.Values.Where(s => s.Name != queryContextName).ToList();
        }

        public IEnumerable<MutationType> GetMutations()
        {
            return mutations.Values.ToList();
        }

        /// <summary>
        /// Remove type and any field that returns that type
        /// </summary>
        /// <typeparam name="TSchemaType"></typeparam>
        public void RemoveTypeAndAllFields<TSchemaType>()
        {
            this.RemoveTypeAndAllFields(typeof(TSchemaType).Name);
        }
        /// <summary>
        /// Remove type and any field that returns that type
        /// </summary>
        /// <param name="typeName"></param>
        public void RemoveTypeAndAllFields(string typeName)
        {
            foreach (var context in types.Values)
            {
                RemoveFieldsOfType(typeName, context);
            }
            types.Remove(typeName);
        }

        private void RemoveFieldsOfType(string typeName, ISchemaType contextType)
        {
            foreach (var field in contextType.GetFields().ToList())
            {
                if (field.GetReturnType(this) == typeName)
                {
                    contextType.RemoveField(field.Name);
                }
            }
        }

        public ISchemaType AddEnum(string name, Type type, string description)
        {
            var schemaType = new SchemaType<object>(this, type, name, description, false, true);
            types.Add(name, schemaType);
            return schemaType.AddAllFields();
        }

        public IDirectiveProcessor GetDirective(string name)
        {
            if (directives.ContainsKey(name))
                return directives[name];
            throw new EntityGraphQLCompilerException($"Directive {name} not defined in schema");
        }
        public void AddDirective(string name, IDirectiveProcessor directive)
        {
            if (directives.ContainsKey(name))
                throw new EntityGraphQLCompilerException($"Directive {name} already exists on schema");
            directives.Add(name, directive);
        }
    }
}