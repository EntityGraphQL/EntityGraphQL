using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using EntityGraphQL.Compiler;
using EntityGraphQL.Directives;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// An interface that the EqlCompiler uses to help understand the types it is building against. This abstraction lets us
    /// have a simple provider that maps directly to an object as well as other complex providers that read a schema from else where
    /// and that can map them back to complex expressions. See ObjectSchemaProvider and MappedSchemaProvider for two examples.
    ///
    /// It works with type name's as strings because although we ultimately build expressions against actual c# types the provider
    /// might expose custom names for the underlying type.
    /// </summary>
    public interface ISchemaProvider
    {
        Func<string, string> SchemaFieldNamer { get; }

        ISchemaType AddType(Type type, string name, string description);

        /// The base context type that expression will be built from. For example your DbContext
        Type ContextType { get; }

        /// Checks if the given type has the given field identifier
        bool TypeHasField(string typeName, string identifier, IEnumerable<string> fieldArgs, ClaimsIdentity claims);
        bool TypeHasField(Type type, string identifier, IEnumerable<string> fieldArgs, ClaimsIdentity claims);

        bool HasType(string typeName);
        bool HasType(Type type);
        ISchemaType Type(string name);
        ISchemaType Type(Type dotnetType);
        List<ISchemaType> EnumTypes();
        /// As EQL is not case sensitive this returns the actual field name in correct casing as defined to build the expression
        string GetActualFieldName(string typeName, string identifier, ClaimsIdentity claims);

        /// <summary>
        /// Given the current context, a type and a field name, it returns the expression for that field. Allows the provider to have a complex expression for a simple field
        /// </summary>
        /// <param name="context"></param>
        /// <param name="typeName"></param>
        /// <param name="fieldName"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        ExpressionResult GetExpressionForField(Expression context, string typeName, string fieldName, Dictionary<string, ExpressionResult> args, ClaimsIdentity claims);
        IEnumerable<ISchemaType> GetScalarTypes();
        /// <summary>
        /// Get the GQL (from schema) type name for a given CLR/dotnet type. Examples int -> Int, int? -> Int
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        string GetSchemaTypeNameForDotnetType(Type type);
        IField GetFieldOnContext(Expression context, string fieldName, ClaimsIdentity claims);
        bool HasMutation(string method);
        string GetGraphQLSchema();
        /// <summary>
        /// Return all the fields that are at the root query type
        /// </summary>
        /// <returns></returns>
        IEnumerable<Field> GetQueryFields();
        /// <summary>
        /// Return all types in the schema
        /// </summary>
        /// <returns></returns>
        IEnumerable<ISchemaType> GetNonContextTypes();

        IEnumerable<MutationType> GetMutations();
        /// <summary>
        /// Add scalar types that the schema will know about when generating schema and introspection.
        /// e.g. schema.AddScalarType(typeof(DateTime), "Date");
        /// </summary>
        /// <param name="clrType">A CLR type that you want mapped</param>
        /// <param name="gqlTypeName">A type name for the scala</param>
        ISchemaType AddScalarType(Type clrType, string gqlTypeName, string description);
        ISchemaType AddScalarType<TType>(string gqlTypeName, string description);
        ISchemaType AddEnum(string name, Type type, string description);

        ISchemaProvider RemoveType<TType>();
        ISchemaProvider RemoveType(string schemaType);
        /// <summary>
        /// Get a directive by name. A directive is used to manipulate or customise a query and/or result
        /// </summary>
        /// <param name="name">name of the directive</param>
        /// <returns></returns>
        IDirectiveProcessor GetDirective(string name);
        void AddDirective(IDirectiveProcessor directive);
        IEnumerable<IDirectiveProcessor> GetDirectives();
        GqlTypeInfo GetCustomTypeMapping(Type dotnetType);
    }
}
