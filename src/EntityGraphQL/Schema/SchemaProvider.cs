using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Directives;
using Microsoft.Extensions.Logging;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Builds and holds your GraphQL schema definition. 
    /// 
    /// The schema definition maps an external view of your data model to you internal dotnet model.
    /// This allows your internal model to change over time while not break your external API. You can create new versions when needed.
    /// </summary>
    /// <typeparam name="TContextType">Base Query object context. Ex. DbContext</typeparam>
    public class SchemaProvider<TContextType> : ISchemaProvider
    {
        public Type QueryContextType { get { return queryType.TypeDotnet; } }
        public Type MutationType { get { return mutationType.SchemaType.TypeDotnet; } }
        public Func<string, string> SchemaFieldNamer { get; }
        public IGqlAuthorizationService AuthorizationService { get; set; }
        protected Dictionary<string, ISchemaType> schemaTypes = new();
        protected Dictionary<string, IDirectiveProcessor> directives = new();
        private readonly QueryCache queryCache;

        public string QueryContextName { get => queryType.Name; }

        private readonly SchemaType<TContextType> queryType;
        private readonly ILogger<SchemaProvider<TContextType>>? logger;
        private readonly GraphQLCompiler graphQLCompiler;
        private readonly bool introspectionEnabled;
        private readonly MutationType mutationType;
        public IDictionary<Type, ICustomTypeConverter> TypeConverters { get; } = new Dictionary<Type, ICustomTypeConverter>();

        // map some types to scalar types
        protected Dictionary<Type, GqlTypeInfo> customTypeMappings;
        public SchemaProvider() : this(null, null) { }
        /// <summary>
        /// Create a new GraphQL Schema provider that defines all the types and fields etc.
        /// </summary>
        /// <param name="fieldNamer">A naming function for fields that will be used when using methods that automatically create field names e.g. SchemaType.AddAllFields()</param>
        public SchemaProvider(IGqlAuthorizationService? authorizationService = null, Func<string, string>? fieldNamer = null, ILogger<SchemaProvider<TContextType>>? logger = null, bool introspectionEnabled = true)
        {
            AuthorizationService = authorizationService ?? new RoleBasedAuthorization();
            SchemaFieldNamer = fieldNamer ?? SchemaBuilderSchemaOptions.DefaultFieldNamer;
            this.logger = logger;
            this.graphQLCompiler = new GraphQLCompiler(this);
            this.introspectionEnabled = introspectionEnabled;
            queryCache = new QueryCache();

            // default GQL scalar types
            schemaTypes.Add("Int", new SchemaType<int>(this, "Int", "Int scalar", null, GqlTypeEnum.Scalar));
            schemaTypes.Add("Float", new SchemaType<double>(this, "Float", "Float scalar", null, GqlTypeEnum.Scalar));
            schemaTypes.Add("Boolean", new SchemaType<bool>(this, "Boolean", "Boolean scalar", null, GqlTypeEnum.Scalar));
            schemaTypes.Add("String", new SchemaType<string>(this, "String", "String scalar", null, GqlTypeEnum.Scalar));
            schemaTypes.Add("ID", new SchemaType<Guid>(this, "ID", "ID scalar", null, GqlTypeEnum.Scalar));
            schemaTypes.Add("Char", new SchemaType<char>(this, "Char", "Char scalar", null, GqlTypeEnum.Scalar));

            // default custom scalar for DateTime
            schemaTypes.Add("Date", new SchemaType<DateTime>(this, "Date", "Date with time scalar", null, GqlTypeEnum.Scalar));

            customTypeMappings = new Dictionary<Type, GqlTypeInfo> {
                {typeof(sbyte), new GqlTypeInfo(() => Type("Int"), typeof(sbyte))},
                {typeof(short), new GqlTypeInfo(() => Type("Int"), typeof(short))},
                {typeof(ushort), new GqlTypeInfo(() => Type("Int"), typeof(ushort))},
                {typeof(long), new GqlTypeInfo(() => Type("Int"), typeof(long))},
                {typeof(ulong), new GqlTypeInfo(() => Type("Int"), typeof(ulong))},
                {typeof(byte), new GqlTypeInfo(() => Type("Int"), typeof(byte))},
                {typeof(uint), new GqlTypeInfo(() => Type("Int"), typeof(uint))},
                {typeof(float), new GqlTypeInfo(() => Type("Float"), typeof(float))},
                {typeof(decimal), new GqlTypeInfo(() => Type("Float"), typeof(decimal))},
                {typeof(byte[]), new GqlTypeInfo(() => Type("String"), typeof(byte[]))},
            };

            var queryContext = new SchemaType<TContextType>(this, "Query", null, null, GqlTypeEnum.Object);
            this.queryType = queryContext;
            schemaTypes.Add(queryContext.Name, queryContext);

            var mutationType = new MutationType(this, "Mutation", null, null);
            this.mutationType = mutationType;
            schemaTypes.Add(mutationType.SchemaType.Name, mutationType.SchemaType);

            if (introspectionEnabled)
            {
                // add types first as fields from the other types may refer to these types
                var typeElement = AddType<Models.TypeElement>("__Type", "Information about types");
                AddType<Models.EnumValue>("__EnumValue", "Information about enums").AddAllFields();
                AddType<Models.InputValue>("__InputValue", "Arguments provided to Fields or Directives and the input fields of an InputObject are represented as Input Values which describe their type and optionally a default value.").AddAllFields();
                AddType<Models.Directive>("__Directive", "Information about directives").AddAllFields();
                AddType<Models.SubscriptionType>("Information about subscriptions").AddAllFields();
                AddType<Models.Field>("__Field", "Information about fields").AddAllFields();
                AddType<Models.Schema>("__Schema", "A GraphQL Schema defines the capabilities of a GraphQL server. It exposes all available types and directives on the server, as well as the entry points for query, mutation, and subscription operations.").AddAllFields();

                // add these fields after the other types as the fields reference those types and by default would auto register under the wrong name
                typeElement.AddAllFields();
                typeElement.ReplaceField("enumValues",
                    new { includeDeprecated = false },
                    (t, p) => t.EnumValues.Where(f => p.includeDeprecated ? f.IsDeprecated || !f.IsDeprecated : !f.IsDeprecated).ToList(),
                    "Enum values available on type"
                );

                SetupIntrospectionTypesAndField();
            }

            var include = new IncludeDirectiveProcessor();
            var skip = new SkipDirectiveProcessor();
            directives.Add(include.Name, include);
            directives.Add(skip.Name, skip);
        }

        /// <summary>
        /// Add a custom type converter to convert query variables into the expected dotnet types. I.e. the incoming variables from 
        /// the request which may be strings or JSON into the dotnet types on the argument classes.
        /// For example a string to DateTime converter.
        /// 
        /// EntityGraphQL already handles Guid, DateTime, InputTypes from the schema, arrays/lists, System.Text.Json elements, float/double/decimal/int/short/uint/long/etc
        /// </summary>
        /// <param name="typeConverter"></param>
        public void AddCustomTypeConverter(ICustomTypeConverter typeConverter)
        {
            TypeConverters.Add(typeConverter.Type, typeConverter);
        }

        private void SetupIntrospectionTypesAndField()
        {
            if (!introspectionEnabled)
                return;

            // evaluate Fields lazily so we don't end up in endless loop
            Type<Models.TypeElement>("__Type").ReplaceField("fields", new { includeDeprecated = false },
                (t, p) => SchemaIntrospection.BuildFieldsForType(this, t.Name!).Where(f => p.includeDeprecated ? f.IsDeprecated || !f.IsDeprecated : !f.IsDeprecated).ToList(), "Fields available on type");

            Query().ReplaceField("__schema", db => SchemaIntrospection.Make(this), "Introspection of the schema").Returns("__Schema");
            Query().ReplaceField("__type", new { name = ArgumentHelper.Required<string>() }, (db, p) => SchemaIntrospection.Make(this).Types.Where(s => s.Name == p.name).First(), "Query a type by name").Returns("__Type");
        }

        /// <summary>
        /// Execute a query using this schema.
        /// </summary>
        /// <param name="gql">The query</param>
        /// <param name="context">The context object. An instance of the context the schema was build from</param>
        /// <param name="serviceProvider">A service provider used for looking up dependencies of field selections and mutations</param>
        /// <param name="user">Optional user/ClaimsPrincipal to check access for queries</param>
        /// <param name="options"></param>
        /// <typeparam name="TContextType"></typeparam>
        /// <returns></returns>
        public QueryResult ExecuteRequest(QueryRequest gql, TContextType context, IServiceProvider? serviceProvider, ClaimsPrincipal? user, ExecutionOptions? options = null)
        {
            return ExecuteRequestAsync(gql, context, serviceProvider, user, options).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Execute a query using this schema.
        /// </summary>
        /// <param name="gql">The query</param>
        /// <param name="context">The context object. An instance of the context the schema was build from</param>
        /// <param name="serviceProvider">A service provider used for looking up dependencies of field selections and mutations</param>
        /// <param name="user">Optional user/ClaimsPrincipal to check access for queries</param>
        /// <param name="options"></param>
        /// <typeparam name="TContextType"></typeparam>
        /// <returns></returns>
        public Task<QueryResult> ExecuteRequestAsync(QueryRequest gql, TContextType context, IServiceProvider? serviceProvider, ClaimsPrincipal? user, ExecutionOptions? options = null)
        {
            QueryResult result;
            try
            {
                if (options == null)
                    options = new ExecutionOptions();
                GraphQLDocument? compiledQuery = null;
                if (options.EnablePersistedQueries)
                {
                    var persistedQuery = (PersistedQueryExtension?)ExpressionUtil.ChangeType(gql.Extensions.GetValueOrDefault("persistedQuery"), typeof(PersistedQueryExtension), null);
                    if (persistedQuery != null && persistedQuery.Version != 1)
                        throw new EntityGraphQLExecutionException("PersistedQueryNotSupported");

                    string? hash = persistedQuery?.Sha256Hash;

                    if (hash == null && gql.Query == null)
                        throw new EntityGraphQLExecutionException("Please provide a persisted query hash or a query string");

                    if (hash != null)
                    {
                        compiledQuery = queryCache.GetCompiledQueryWithHash(hash);
                        if (compiledQuery == null && gql.Query == null)
                            throw new EntityGraphQLExecutionException("PersistedQueryNotFound");
                        else if (compiledQuery == null)
                        {
                            compiledQuery = graphQLCompiler.Compile(gql, new QueryRequestContext(AuthorizationService, user));
                            queryCache.AddCompiledQuery(hash, compiledQuery);
                        }
                    }
                    else if (compiledQuery == null)
                    {
                        // if here they sent query with no hash
                        // persisted queries will not auto cache it (only when you provide the hash) but is QueryCache is enabled we will cache it
                        if (options.EnableQueryCache)
                            compiledQuery = CompileQueryWithCache(gql, user);
                        else
                            compiledQuery = graphQLCompiler.Compile(gql, new QueryRequestContext(AuthorizationService, user));
                    }
                }
                else if (options.EnableQueryCache)
                {
                    compiledQuery = CompileQueryWithCache(gql, user);
                }
                else
                {
                    // no cache
                    if (gql.Query == null)
                    {
                        string? hash = ((PersistedQueryExtension?)ExpressionUtil.ChangeType(gql.Extensions.GetValueOrDefault("persistedQuery"), typeof(PersistedQueryExtension), null))?.Sha256Hash;
                        if (hash != null)
                            throw new EntityGraphQLExecutionException("PersistedQueryNotSupported");

                        throw new ArgumentNullException(nameof(gql.Query), "Query must be set unless you are using persisted queries");
                    }

                    compiledQuery = graphQLCompiler.Compile(gql, new QueryRequestContext(AuthorizationService, user));
                }

                result = compiledQuery.ExecuteQuery(context, serviceProvider, gql.Variables, gql.OperationName, options);
            }
            catch (EntityGraphQLValidationException ex)
            {
                logger?.LogError(ex, "Error executing QueryRequest");
                result = new QueryResult();
                ex.ValidationErrors.ForEach(e => result.AddError(e));
            }
            catch (AggregateException aex)
            {
                logger?.LogError(aex, "Error executing QueryRequest");
                result = new QueryResult();

                foreach (var ex in aex.InnerExceptions)
                {
                    if (ex is EntityGraphQLValidationException exception)
                        exception.ValidationErrors.ForEach(e => result.AddError(e));
                    else if (ex is EntityGraphQLException gqlException)
                        result.AddError(ex.Message, gqlException.Extensions);
                    else
                        result.AddError(ex.Message);
                }
            }
            catch (EntityGraphQLException ex)
            {
                result = new QueryResult();
                result.AddError(ex.Message, ex.Extensions);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error executing QueryRequest");
                // error with the whole query
                result = new QueryResult(new GraphQLError(ex.Message, null));
            }

            return Task.FromResult(result);
        }

        private GraphQLDocument CompileQueryWithCache(QueryRequest gql, ClaimsPrincipal? user)
        {
            GraphQLDocument? compiledQuery;
            // try to get the compiled query from the cache
            // cache the result
            if (gql.Query == null)
            {
                string? phash = ((PersistedQueryExtension?)ExpressionUtil.ChangeType(gql.Extensions.GetValueOrDefault("persistedQuery"), typeof(PersistedQueryExtension), null))?.Sha256Hash;
                if (phash != null)
                    throw new EntityGraphQLExecutionException("PersistedQueryNotSupported");
                throw new ArgumentNullException(nameof(gql.Query), "Query must be set unless you are using persisted queries");
            }

            (compiledQuery, var hash) = queryCache.GetCompiledQuery(gql.Query, null);
            if (compiledQuery == null)
            {
                compiledQuery = graphQLCompiler.Compile(gql, new QueryRequestContext(AuthorizationService, user));
                queryCache.AddCompiledQuery(hash, compiledQuery);
            }

            return compiledQuery;
        }

        /// <summary>
        /// Add a new type into the schema with TBaseType as its context
        /// </summary>
        /// <param name="name">Name of the type</param>
        /// <param name="description">description of the type</param>
        /// <typeparam name="TBaseType"></typeparam>
        /// <returns>The added type for further changes via chaining</returns>
        public SchemaType<TBaseType> AddType<TBaseType>(string name, string? description)
        {
            var gqlType = typeof(TBaseType).IsAbstract || typeof(TBaseType).IsInterface ? GqlTypeEnum.Interface : GqlTypeEnum.Object;
            var schemaType = new SchemaType<TBaseType>(this, name, description, null, gqlType);
            FinishAddingType(typeof(TBaseType), name, schemaType);
            return schemaType;
        }

        /// <summary>
        /// Add a new type into the schema with contextType as its context
        /// </summary>
        /// <param name="contextType"></param>
        /// <param name="name">Name of the type</param>
        /// <param name="description">description of the type</param>
        /// <returns>The added type for further changes via chaining</returns>
        public ISchemaType AddType(Type contextType, string name, string? description)
        {
            var gqlType = contextType.IsAbstract || contextType.IsInterface ? GqlTypeEnum.Interface : GqlTypeEnum.Object;
            var newType = (ISchemaType)Activator.CreateInstance(typeof(SchemaType<>).MakeGenericType(contextType), this, contextType, name, description, null, gqlType, null)!;
            FinishAddingType(contextType, name, newType);
            return newType;
        }

        /// <summary>
        /// Add a new type into the schema with TBaseType as its context
        /// </summary>
        /// <typeparam name="TBaseType"></typeparam>
        /// <param name="name">Name of the type</param>
        /// <param name="description">description of the type</param>
        /// <param name="updateFunc">Callback called with the schema type where you can further update the type</param>
        public void AddType<TBaseType>(string name, string description, Action<SchemaType<TBaseType>> updateFunc)
        {
            updateFunc(AddType<TBaseType>(name, description));
        }

        /// <summary>
        /// Adds a new type into the schema. The name defaults to the TBaseType name
        /// </summary>
        /// <param name="description">description of the type</param>
        /// <typeparam name="TBaseType"></typeparam>
        /// <returns>The added type for further changes via chaining</returns>
        public SchemaType<TBaseType> AddType<TBaseType>(string description)
        {
            var name = typeof(TBaseType).Name;
            return AddType<TBaseType>(name, description);
        }

        private void FinishAddingType(Type contextType, string name, ISchemaType tt)
        {
            tt.RequiredAuthorization = AuthorizationService.GetRequiredAuthFromType(contextType);
            if (string.IsNullOrEmpty(tt.Description))
            {
                tt.Description = contextType.GetCustomAttribute<DescriptionAttribute>()?.Description;
            }
            schemaTypes.Add(name, tt);
        }

        /// <summary>
        /// Remove TType from the schema
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <returns>The SchemaProvider</returns>
        public ISchemaProvider RemoveType<TType>()
        {
            return RemoveType(GetSchemaType(typeof(TType), null).Name);
        }

        /// <summary>
        /// Remove TType from the schema
        /// </summary>
        /// <param name="schemaType"></param>
        /// <returns>The SchemaProvider</returns>
        public ISchemaProvider RemoveType(string schemaType)
        {
            schemaTypes.Remove(schemaType);
            return this;
        }

        /// <summary>
        /// Add a GraphQL Input type to the schema. Input types are objects used in arguments of fields or mutations
        /// </summary>
        /// <param name="name">Name of the type. Used as passed. Case sensitive</param>
        /// <param name="description">Description of the input type</param>
        /// <typeparam name="TBaseType"></typeparam>
        /// <returns>The added type for further changes via chaining</returns>
        public SchemaType<TBaseType> AddInputType<TBaseType>(string name, string? description)
        {
            return (SchemaType<TBaseType>)AddInputType(typeof(TBaseType), name, description);
        }

        /// <summary>
        /// Add a GraphQL Input type to the schema. Input types are objects used in arguments of fields or mutations
        /// </summary>
        /// <param name="name">Name of the type. Used as passed. Case sensitive</param>
        /// <param name="description">Description of the input type</param>
        /// <returns>The added type for further changes via chaining</returns>
        public ISchemaType AddInputType(Type type, string name, string? description)
        {
            var newType = (ISchemaType)Activator.CreateInstance(typeof(SchemaType<>).MakeGenericType(type), this, type, name, description, null, GqlTypeEnum.Input, null)!;
            FinishAddingType(type, name, newType);

            return newType;
        }

        /// <summary>
        /// Adds a new scalar type defined to the schema. Dotnet types of clrType will be treated as gqlTypeName
        /// </summary>
        /// <param name="clrType">Dotnet type to mapp to a scalar</param>
        /// <param name="gqlTypeName">GraphQL scalar type name</param>
        /// <param name="description">Description of the scalar type</param>
        /// <returns>The added type for further changes via chaining</returns>
        public ISchemaType AddScalarType(Type clrType, string gqlTypeName, string? description)
        {
            var schemaType = (ISchemaType)Activator.CreateInstance(typeof(SchemaType<>).MakeGenericType(clrType), this, gqlTypeName, description, null, GqlTypeEnum.Scalar, null)!;
            schemaTypes.Add(gqlTypeName, schemaType);
            return schemaType;
        }

        /// <summary>
        /// Adds a new scalar type defined to the schema. Dotnet types of TType will be treated as gqlTypeName
        /// </summary>
        /// <param name="clrType">Dotnet type to mapp to a scalar</param>
        /// <param name="gqlTypeName">GraphQL scalar type name</param>
        /// <param name="description">Description of the scalar type</param>
        /// <returns>The added type for further changes via chaining</returns>
        public SchemaType<TType> AddScalarType<TType>(string gqlTypeName, string? description)
        {
            var schemaType = new SchemaType<TType>(this, gqlTypeName, description, null, GqlTypeEnum.Scalar);
            schemaTypes.Add(gqlTypeName, schemaType);
            return schemaType;
        }

        /// <summary>
        /// Add any methods marked with GraphQLMutationAttribute in the given object to the schema. Method names are added as using fieldNamer
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <param name="autoAddInputTypes">If true, any class types seen in the mutation argument properties will be added to the schema</param>
        /// <param name="addNonAttributedMethods">If true, add any method in the mutation class even if it isn't marked with the mutation attribute</param>
        public void AddMutationsFrom<TType>(bool autoAddInputTypes = false, bool addNonAttributedMethods = false) where TType : class
        {
            mutationType.AddFrom<TType>(autoAddInputTypes, addNonAttributedMethods);
        }

        /// <summary>
        /// Search for a GraphQL type with the given name. Lookup is only done by name.
        /// 
        /// Customer type mappings are not searched
        /// 
        /// Use the Type<T>() methods for returning typed SchemaType<T> 
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns>An ISchemaType if the type is found in the schema</returns>
        /// <exception cref="EntityGraphQLCompilerException">If type name not found</exception>
        public ISchemaType GetSchemaType(string typeName, QueryRequestContext? requestContext)
        {
            if (schemaTypes.ContainsKey(typeName))
                return CheckTypeAccess(schemaTypes[typeName], requestContext);

            throw new EntityGraphQLCompilerException($"Type {typeName} not found in schema");
        }

        /// <summary>
        /// Search for a GraphQL type with the given name. Lookup is done by DotNet type first.
        /// Then by the type name. Then the customTypeMappings are searched
        /// 
        /// Use the Type<T>() methods for returning typed SchemaType<T> 
        /// </summary>
        /// <param name="dotnetType"></param>
        /// <returns></returns>
        /// <exception cref="EntityGraphQLCompilerException"></exception>
        public ISchemaType GetSchemaType(Type dotnetType, QueryRequestContext? requestContext)
        {
            // look up by the actual type not the name
            var schemaType = schemaTypes.Values.FirstOrDefault(t => t.TypeDotnet == dotnetType)
                ?? schemaTypes.GetValueOrDefault(dotnetType.Name);

            if (schemaType == null && customTypeMappings.ContainsKey(dotnetType))
            {
                schemaType = customTypeMappings[dotnetType].SchemaType;
            }
            if (schemaType == null)
                throw new EntityGraphQLCompilerException($"No schema type found for dotnet type {dotnetType.Name}. Make sure you add it or add a type mapping");
            return CheckTypeAccess(schemaType, requestContext);
        }

        private ISchemaType CheckTypeAccess(ISchemaType schemaType, QueryRequestContext? requestContext)
        {
            if (requestContext == null)
                return schemaType;

            if (requestContext.AuthorizationService != null && !requestContext.AuthorizationService.IsAuthorized(requestContext.User, schemaType.RequiredAuthorization))
                throw new EntityGraphQLAccessException($"You are not authorized to access the '{schemaType.Name}' type.");

            return schemaType;
        }

        /// <summary>
        /// Get registered type by TType name
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <returns>The typed SchemaType<TType> object for configuration</returns>
        public SchemaType<TType> Type<TType>()
        {
            return (SchemaType<TType>)GetSchemaType(typeof(TType), null);
        }
        /// <summary>
        /// Get registered type by schema type name
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <param name="typeName"></param>
        /// <returns>The typed SchemaType<TType> object for configuration</returns>
        public SchemaType<TType> Type<TType>(string typeName)
        {
            return (SchemaType<TType>)GetSchemaType(typeName, null);
        }

        /// <summary>
        /// Search for a GraphQL type with the given name. Lookup is only done by name.
        /// 
        /// Customer type mappings are not searched
        /// 
        /// Use the Type<T>() methods for returning typed SchemaType<T> 
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns>An ISchemaType if the type is found in the schema</returns>
        /// <exception cref="EntityGraphQLCompilerException">If type name not found</exception>
        public ISchemaType Type(string typeName)
        {
            return GetSchemaType(typeName, null);
        }

        /// <summary>
        /// Find a schema type for TType and update the type with the configure callback
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <param name="configure"></param>
        public void UpdateType<TType>(Action<SchemaType<TType>> configure) => configure(Type<TType>());

        /// <summary>
        /// Returns true if the schema has a type by name defined
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns>True if found. False if not</returns>
        public bool HasType(string typeName)
        {
            return schemaTypes.ContainsKey(typeName);
        }

        /// <summary>
        /// Returns true if the schema has a type that has the given dotnet type as its base context
        /// </summary>
        /// <param name="type"></param>
        /// <returns>True if found. False if not</returns>
        public bool HasType(Type type)
        {
            if (type == queryType.TypeDotnet)
                return true;

            foreach (var schemaType in schemaTypes.Values)
            {
                if (schemaType.TypeDotnet == type)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Add a mapping from a Dotnet type to a GraphQL schema type name. Make sure you have added the GraphQL type
        /// in the schema as a Scalar type or full type
        /// 
        /// E.g. schema.AddTypeMapping<NpgsqlPolygon>("[Point!]!");
        /// </summary>
        /// <param name="gqlType">The GraphQL schema type in full form. E.g. [Int!]!, [Int], Int, etc.</param>
        /// <typeparam name="TFromType"></typeparam>
        public void AddTypeMapping<TFromType>(string gqlType)
        {
            var typeInfo = GqlTypeInfo.FromGqlType(this, typeof(TFromType), gqlType);
            // add mapping
            customTypeMappings.Add(typeof(TFromType), typeInfo);
            SetupIntrospectionTypesAndField();
        }

        /// <summary>
        /// Get the GqlTypeInfo for a custom type mapping of dotnetType.
        /// Returns null if there is no mapping
        /// </summary>
        /// <param name="dotnetType"></param>
        /// <returns></returns>
        public GqlTypeInfo? GetCustomTypeMapping(Type dotnetType)
        {
            if (customTypeMappings.ContainsKey(dotnetType))
                return customTypeMappings[dotnetType];
            return null;
        }

        /// <summary>
        /// Return the Root Query schema type. Use this to add/remove/modify fields to the root query
        /// </summary>
        /// <returns>Root query schema type</returns>
        public SchemaType<TContextType> Query() => queryType;

        /// <summary>
        /// Provide a callback to update the root query schema type - add/remove/modify fields to the root query
        /// </summary>
        public void UpdateQuery(Action<SchemaType<TContextType>> configure) => configure(queryType);

        /// <summary>
        /// Return the Root Mutation schema type. Use this to add/remove/modify mutation fields
        /// </summary>
        /// <returns>Root mutation schema type</returns>
        public MutationType Mutation() => mutationType;

        /// <summary>
        /// Provide a callback to update the root query schema type - add/remove/modify fields to the root query
        /// </summary>
        public void UpdateMutation(Action<MutationType> configure) => configure(mutationType);

        /// <summary>
        /// Add an Enum type to the schema
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        public ISchemaType AddEnum(string name, Type type, string description)
        {
            var schemaType = (ISchemaType)Activator.CreateInstance(typeof(SchemaType<>).MakeGenericType(type), this, type, name, description, null, GqlTypeEnum.Enum, null)!;
            FinishAddingType(type, name, schemaType);
            return schemaType.AddAllFields();
        }

        /// <summary>
        /// Add an interface type to the schema
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        public ISchemaType AddInterface(Type type, string name, string? description)
        {
            var schemaType = (ISchemaType)Activator.CreateInstance(typeof(SchemaType<>).MakeGenericType(type), this, type, name, description, null, GqlTypeEnum.Interface, null)!;
            FinishAddingType(type, name, schemaType);
            return schemaType;
        }

        /// <summary>
        /// Add an interface type to the schema
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <returns></returns>
        public ISchemaType AddInterface<TInterface>(string name, string? description)
        {
            var schemaType = new SchemaType<TInterface>(this, typeof(TInterface), name, description, null, GqlTypeEnum.Interface);
            FinishAddingType(typeof(TInterface), name, schemaType);
            return schemaType;
        }

        /// <summary>
        /// Return a list of all Enum types in the schema
        /// </summary>
        /// <returns></returns>
        public List<ISchemaType> GetEnumTypes()
        {
            return schemaTypes.Values.Where(t => t.IsEnum).ToList();
        }

        /// <summary>
        /// Builds a GraphQL schema definition from the schema.
        /// </summary>
        /// <returns>String containing the schema definition</returns>
        public string ToGraphQLSchemaString()
        {
            return SchemaGenerator.Make(this);
        }

        /// <summary>
        /// Return a list of types in the schema that are not the root context Query type
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ISchemaType> GetNonContextTypes()
        {
            return schemaTypes.Values.Where(s => s.Name != queryType.Name).ToList();
        }

        /// <summary>
        /// Return a list of all scalar types in the schema
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ISchemaType> GetScalarTypes()
        {
            return schemaTypes.Values.Where(t => t.IsScalar);
        }

        /// <summary>
        /// Remove type TSchemaType and any field that returns that type
        /// </summary>
        /// <typeparam name="TSchemaType"></typeparam>
        public void RemoveTypeAndAllFields<TSchemaType>()
        {
            RemoveTypeAndAllFields(typeof(TSchemaType).Name);
        }
        /// <summary>
        /// Remove type by name and any field that returns that type
        /// </summary>
        /// <param name="typeName"></param>
        public void RemoveTypeAndAllFields(string typeName)
        {
            foreach (var context in schemaTypes.Values)
            {
                RemoveFieldsOfType(typeName, context);
            }
            schemaTypes.Remove(typeName);
        }

        private void RemoveFieldsOfType(string schemaType, ISchemaType contextType)
        {
            foreach (var field in contextType.GetFields())
            {
                try
                {
                    if (field.ReturnType.SchemaType.Name == schemaType)
                    {
                        contextType.RemoveField(field.Name);
                    }
                }
                catch (EntityGraphQLCompilerException)
                {
                    // SchemaType looks up the type in the schema. And there is a chance that type is not in there
                    // either not added or removed previously
                }
            }
        }

        /// <summary>
        /// Search for a directive by name. Throws exception if not found
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="EntityGraphQLCompilerException"></exception>
        public IDirectiveProcessor GetDirective(string name)
        {
            if (directives.ContainsKey(name))
                return directives[name];
            throw new EntityGraphQLCompilerException($"Directive {name} not defined in schema");
        }
        /// <summary>
        /// Return a list of directives in the schema
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IDirectiveProcessor> GetDirectives()
        {
            return directives.Values.ToList();
        }
        /// <summary>
        /// Add a directive to the schema
        /// </summary>
        /// <param name="directive"></param>
        /// <exception cref="EntityGraphQLCompilerException"></exception>
        public void AddDirective(IDirectiveProcessor directive)
        {
            if (directives.ContainsKey(directive.Name))
                throw new EntityGraphQLCompilerException($"Directive {directive.Name} already exists on schema");
            directives.Add(directive.Name, directive);
        }

        /// <summary>
        /// Build the Query schema by reflection on the context type. Same as SchemaBuilder.FromObject<T>()
        /// </summary>
        /// <param name="options"></param>
        public void PopulateFromContext(SchemaBuilderOptions? options = null)
        {
            if (options == null)
                options = new SchemaBuilderOptions();
            SchemaBuilder.FromObject(this, options);
        }
    }
}