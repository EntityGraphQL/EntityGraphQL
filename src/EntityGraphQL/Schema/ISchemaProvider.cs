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
        Type ContextType { get; }
        Func<string, string> SchemaFieldNamer { get; }
        IGqlAuthorizationService AuthorizationService { get; set; }
        string QueryContextName { get; }

        void AddDirective(IDirectiveProcessor directive);
        ISchemaType AddEnum(string name, Type type, string description);
        SchemaType<TBaseType> AddInputType<TBaseType>(string name, string? description);
        ISchemaType AddInputType(Type type, string name, string? description);
        void AddMutationFrom<TType>(TType mutationClassInstance) where TType : notnull;
        ISchemaType AddScalarType(Type clrType, string gqlTypeName, string? description);
        SchemaType<TType> AddScalarType<TType>(string gqlTypeName, string? description);
        SchemaType<TBaseType> AddType<TBaseType>(string name, string? description);
        ISchemaType AddType(Type contextType, string name, string? description);
        void AddType<TBaseType>(string name, string description, Action<SchemaType<TBaseType>> updateFunc);
        SchemaType<TBaseType> AddType<TBaseType>(string description);
        void AddTypeMapping<TFromType>(string gqlType);
        IField GetActualField(string typeName, string identifier, QueryRequestContext? requestContext);
        GqlTypeInfo? GetCustomTypeMapping(Type dotnetType);
        IDirectiveProcessor GetDirective(string name);
        IEnumerable<IDirectiveProcessor> GetDirectives();
        List<ISchemaType> GetEnumTypes();
        IEnumerable<MutationType> GetMutations();
        IEnumerable<ISchemaType> GetNonContextTypes();
        IEnumerable<ISchemaType> GetScalarTypes();
        ISchemaType GetSchemaType(string typeName);
        ISchemaType GetSchemaType(Type dotnetType);
        ISchemaType GetSchemaTypeForDotnetType(Type type);
        Type GetTypeFromMutationReturn(Type type);
        bool HasMutation(string mutationName);
        bool HasType(string typeName);
        bool HasType(Type type);
        void PopulateFromContext(bool autoCreateIdArguments, bool autoCreateEnumTypes);
        ISchemaProvider RemoveType<TType>();
        ISchemaProvider RemoveType(string schemaType);
        void RemoveTypeAndAllFields<TSchemaType>();
        void RemoveTypeAndAllFields(string typeName);
        string ToGraphQLSchemaString();
        SchemaType<TType> Type<TType>();
        SchemaType<TType> Type<TType>(string typeName);
        ISchemaType Type(string typeName);
        void UpdateType<TType>(Action<SchemaType<TType>> configure);
    }
}
