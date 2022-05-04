using System;
using System.Collections.Generic;

namespace EntityGraphQL.Schema
{
    public interface ISchemaType
    {
        Type TypeDotnet { get; }
        string Name { get; }
        string? Description { get; set; }
        bool IsInput { get; }
        bool IsInterface{ get; }
        bool IsEnum { get; }
        bool IsScalar { get; }
        string? Extends { get; }
        RequiredAuthorization? RequiredAuthorization { get; set; }
        IField GetField(string identifier, QueryRequestContext? requestContext);
        IEnumerable<IField> GetFields();
        bool HasField(string identifier, QueryRequestContext? requestContext);
        ISchemaType AddAllFields(bool autoCreateNewComplexTypes = false, bool autoCreateEnumTypes = true);
        void AddFields(IEnumerable<IField> fields);
        IField AddField(IField field);
        void RemoveField(string name);
    }
}