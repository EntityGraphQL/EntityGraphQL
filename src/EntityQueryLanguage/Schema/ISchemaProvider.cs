using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityQueryLanguage.Compiler;

namespace EntityQueryLanguage.Schema
{
    /// An interface that the EqlCompiler uses to help understand the types it is building against. This abstraction lets us
    /// have a simple provider that maps directly to an object as well as other complex providers that read a schema from else where
    /// and that can map them back to complex expressions. See ObjectSchemaProvider and MappedSchemaProvider for two examples.
    ///
    /// It works with type name's as strings because although we ultimately build expressions against actual c# types the provider
    /// might expose custom names for the underlying type.
    public interface ISchemaProvider
    {
        /// The base context type that expression will be built from. For example your DbContext
        Type ContextType { get; }
        /// Checks if the given type has the given field identifier
        bool TypeHasField(string typeName, string identifier);
        bool TypeHasField(Type type, string identifier);

        bool HasType(string typeName);
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
        ExpressionResult GetExpressionForField(Expression context, string typeName, string field, Dictionary<string, ExpressionResult> args);
        string GetSchemaTypeNameForRealType(Type type);
        IMethodType GetMethodType(Expression context, string field);
        bool HasMutation(string method);
    }
}
