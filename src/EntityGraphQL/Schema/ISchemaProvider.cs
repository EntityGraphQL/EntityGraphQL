using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;

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
        /// The base context type that expression will be built from. For example your DbContext
        Type ContextType { get; }
        IEnumerable<string> CustomScalarTypes { get; }

        /// Checks if the given type has the given field identifier
        bool TypeHasField(string typeName, string identifier, IEnumerable<string> fieldArgs);
        bool TypeHasField(Type type, string identifier, IEnumerable<string> fieldArgs);

        bool HasType(string typeName);
        bool HasType(Type type);
        ISchemaType Type(string name);
        /// As EQL is not case sensitive this returns the actual field name in correct casing as defined to build the expression
        string GetActualFieldName(string typeName, string identifier);

        /// <summary>
        /// Given the current context, a type and a field name, it returns the expression for that field. Allows the provider to have a complex expression for a simple field
        /// </summary>
        /// <param name="context"></param>
        /// <param name="typeName"></param>
        /// <param name="field"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        ExpressionResult GetExpressionForField(Expression context, string typeName, string fieldName, Dictionary<string, ExpressionResult> args);
        string GetSchemaTypeNameForRealType(Type type);
        IMethodType GetFieldType(Expression context, string fieldName);
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

        IEnumerable<IMethodType> GetMutations();
        /// <summary>
        /// Add custom scalar types that the schema will know about when generating schema and introspection.
        /// e.g. schema.AddCustomScalarType(typeof(DateTime), "Date");
        /// </summary>
        /// <param name="clrType">A CLR type that you want mapped</param>
        /// <param name="gqlTypeName">A type name for the scala</param>
        void AddCustomScalarType(Type clrType, string gqlTypeName);
    }
}
