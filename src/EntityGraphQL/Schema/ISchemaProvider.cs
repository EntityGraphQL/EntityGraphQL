using System;
using System.Collections.Generic;
using EntityGraphQL.Directives;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// An interface that the Compiler uses to help understand the types it is building against. This abstraction lets us
    /// have a simple provider that maps directly to an object as well as other complex providers that read a schema from else where
    /// and that can map them back to complex expressions. See SchemaProvider for implementation.
    ///
    /// It works with type name's as strings because although we ultimately build expressions against actual C# types the provider
    /// might expose custom names for the underlying type.
    /// </summary>
    public interface ISchemaProvider
    {
        Type QueryContextType { get; }
        Type MutationType { get; }
        Type SubscriptionType { get; }
        Func<string, string> SchemaFieldNamer { get; }
        IGqlAuthorizationService AuthorizationService { get; set; }
        string QueryContextName { get; }
        IDictionary<Type, ICustomTypeConverter> TypeConverters { get; }

        void AddDirective(IDirectiveProcessor directive);
        ISchemaType AddEnum(string name, Type type, string description);
        ISchemaType AddInterface<TInterface>(string name, string? description);
        ISchemaType AddInterface(Type type, string name, string? description);
        ISchemaType AddUnion(Type type, string name, string? description);
        SchemaType<TBaseType> AddInputType<TBaseType>(string name, string? description);
        ISchemaType AddInputType(Type type, string name, string? description);
        void AddMutationsFrom<TType>(SchemaBuilderOptions? options = null) where TType : class;
        ISchemaType AddScalarType(Type clrType, string gqlTypeName, string? description);
        SchemaType<TType> AddScalarType<TType>(string gqlTypeName, string? description);
        SchemaType<TBaseType> AddType<TBaseType>(string name, string? description);
        ISchemaType AddType(Type contextType, string name, string? description);
        SchemaType<TBaseType> AddType<TBaseType>(string name, string description, Action<SchemaType<TBaseType>> updateFunc);
        SchemaType<TBaseType> AddType<TBaseType>(string description);
        ISchemaType AddType(ISchemaType schemaType);
        void AddTypeMapping<TFromType>(string gqlType);
        GqlTypeInfo? GetCustomTypeMapping(Type dotnetType);
        IDirectiveProcessor GetDirective(string name);
        IEnumerable<IDirectiveProcessor> GetDirectives();
        List<ISchemaType> GetEnumTypes();
        IEnumerable<ISchemaType> GetNonContextTypes();
        IEnumerable<ISchemaType> GetScalarTypes();
        IExtensionAttributeHandler? GetAttributeHandlerFor(Type attributeType);
        ISchemaProvider AddAttributeHandler(IExtensionAttributeHandler handler);
        ISchemaType GetSchemaType(string typeName, QueryRequestContext? requestContext);
        ISchemaType GetSchemaType(Type dotnetType, QueryRequestContext? requestContext);
        bool HasType(string typeName);
        bool HasType(Type type);
        void PopulateFromContext(SchemaBuilderOptions? options = null);
        ISchemaProvider RemoveType<TType>();
        ISchemaProvider RemoveType(string schemaType);
        void RemoveTypeAndAllFields<TSchemaType>();
        void RemoveTypeAndAllFields(string typeName);
        string ToGraphQLSchemaString();
        SchemaType<TType> Type<TType>();
        SchemaType<TType> Type<TType>(string typeName);
        ISchemaType Type(string typeName);
        ISchemaType Type(Type type);
        SchemaType<TType> Type<TType>(Type type);
        void UpdateType<TType>(Action<SchemaType<TType>> configure);
        MutationType Mutation();
        SubscriptionType Subscription();
    }
}
