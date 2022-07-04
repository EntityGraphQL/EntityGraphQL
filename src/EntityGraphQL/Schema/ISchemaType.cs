using System;
using System.Collections.Generic;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Represents a type in the schema
    /// </summary>
    public interface ISchemaType
    {
        /// <summary>
        /// The Dotnet type the schema type maps to
        /// </summary>
        Type TypeDotnet { get; }
        /// <summary>
        /// The GraphQL type - Scalar, InputObject, Interface, Enum, Object, etc
        /// </summary>
        GqlTypeEnum GqlType { get; }
        string Name { get; }
        string? Description { get; set; }
        /// <summary>
        /// True if GqlType is GqlTypeEnum.Input
        /// </summary>
        bool IsInput { get; }
        /// <summary>
        /// True if GqlType is GqlTypeEnum.Interface
        /// </summary>
        bool IsInterface { get; }
        /// <summary>
        /// True if GqlType is GqlTypeEnum.Enum
        /// </summary>
        bool IsEnum { get; }
        /// <summary>
        /// True if GqlType is GqlTypeEnum.Scalar
        /// </summary>
        bool IsScalar { get; }
        /// <summary>
        /// If the type in a query requires a selection {  }
        /// </summary>
        bool RequiresSelection { get; }
        IList<ISchemaType> BaseTypes { get; }
        RequiredAuthorization? RequiredAuthorization { get; set; }
        IField GetField(string identifier, QueryRequestContext? requestContext);
        IEnumerable<IField> GetFields();
        bool HasField(string identifier, QueryRequestContext? requestContext);
        ISchemaType AddAllFields(bool autoCreateNewComplexTypes = false, bool autoCreateEnumTypes = true);
        void AddFields(IEnumerable<IField> fields);
        IField AddField(IField field);
        void RemoveField(string name);
        ISchemaType AddAllBaseTypes();
        ISchemaType AddBaseType<TClrType>();
        ISchemaType AddBaseType(string name);
    }
}