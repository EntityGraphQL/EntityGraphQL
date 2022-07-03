using System;
using System.Collections.Generic;

namespace EntityGraphQL.Schema
{
    public interface ISchemaType
    {
        Type TypeDotnet { get; }
        GqlTypeEnum GqlType { get; }
        string Name { get; }
        string? Description { get; set; }
        bool IsInput { get; }
        bool IsInterface { get; }
        bool IsEnum { get; }
        bool IsScalar { get; }
        bool RequiresSelection { get; }
        [Obsolete("Multiple base types are now supported. Use BaseTypes")]
        string? BaseType { get; }
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