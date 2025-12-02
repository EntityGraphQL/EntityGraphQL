using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Directives;

namespace EntityGraphQL.Schema;

// Generic TryConvert delegate for custom converters

/// <summary>
/// A delegate for trying to convert from one type to another with an out parameter for the result
/// </summary>
/// <typeparam name="TFrom">type to convert from</typeparam>
/// <typeparam name="TTo">type to convert to</typeparam>
public delegate bool TypeConverterTryFromTo<in TFrom, TTo>(TFrom value, ISchemaProvider schema, out TTo result);

/// <summary>
/// A delegate for trying to convert from object to a specific type with an out parameter for the result
/// </summary>
/// <typeparam name="TTo">type to convert to</typeparam>
public delegate bool TypeConverterTryTo<TTo>(object? value, Type toType, ISchemaProvider schema, out TTo result);

/// <summary>
/// A delegate for trying to convert from a specific type to object with an out parameter for the result
/// </summary>
/// <typeparam name="TFrom">type to convert from</typeparam>
public delegate bool TypeConverterTryFrom<in TFrom>(TFrom value, Type toType, ISchemaProvider schema, out object? result);

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
    EqlMethodProvider MethodProvider { get; }

    ISchemaProvider AddCustomTypeConverter<TFrom, TTo>(Func<TFrom, ISchemaProvider, TTo> convert);
    ISchemaProvider AddCustomTypeConverter<TFrom, TTo>(TypeConverterTryFromTo<TFrom, TTo> tryConvert);
    ISchemaProvider AddCustomTypeConverter<TTo>(Func<object?, ISchemaProvider, TTo> convert);
    ISchemaProvider AddCustomTypeConverter<TTo>(TypeConverterTryTo<TTo> tryConvert);
    ISchemaProvider AddCustomTypeConverter<TFrom>(Func<TFrom, Type, ISchemaProvider, object?> convert, params Type[] supportedToTypes);
    ISchemaProvider AddCustomTypeConverter<TFrom>(TypeConverterTryFrom<TFrom> tryConvert, params Type[] supportedToTypes);

    // Attempts to convert the value using custom converters (from-to first, then to-only, then from-only).
    bool TryConvertCustom(object? value, Type toType, out object? result);

    void AddDirective(IDirectiveProcessor directive);
    ISchemaType AddEnum(string name, Type type, string description);
    SchemaType<TEnum> AddEnum<TEnum>(string name, string description);
    ISchemaType AddInterface<TInterface>(string name, string? description);
    ISchemaType AddInterface(Type type, string name, string? description);
    ISchemaType AddUnion(Type type, string name, string? description);
    SchemaType<TBaseType> AddInputType<TBaseType>(string name, string? description);
    ISchemaType AddInputType(Type type, string name, string? description);
    void AddMutationsFrom<TType>(SchemaBuilderOptions? options = null)
        where TType : class;
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
    ISchemaType GetSchemaType(Type dotnetType, bool inputTypesOnly, QueryRequestContext? requestContext);
    bool TryGetSchemaType(Type dotnetType, bool inputTypesOnly, out ISchemaType? schemaType, QueryRequestContext? requestContext);
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

    /// <summary>
    /// Call this to validate that the schema contains all the information it needs. As fields and Types can be added out of
    /// order to the schema, this lets you validate that the schema is complete preventing you from getting the errors at
    /// runtime during a query.
    ///
    /// Throws an EntityGraphQLCompilerException if the schema is not valid
    /// </summary>
    void Validate();
    ISchemaType CheckTypeAccess(ISchemaType schemaType, QueryRequestContext? requestContext);
    IEnumerable<GraphQLError> GenerateErrors(Exception exception, string? fieldName = null);
    string AllowedExceptionMessage(Exception exception, string? fieldName = null);
}
