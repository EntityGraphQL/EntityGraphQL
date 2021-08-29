using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace EntityGraphQL.Schema
{
    public interface ISchemaType
    {
        Type TypeDotnet { get; }
        string Name { get; }
        string Description { get; }
        bool IsInput { get; }
        bool IsEnum { get; }
        bool IsScalar { get; }
        RequiredClaims AuthorizeClaims { get; set; }
        Field GetField(string identifier, ClaimsIdentity claims);
        IEnumerable<Field> GetFields();
        bool HasField(string identifier);
        void AddFields(List<Field> fields);
        Field AddField(Field field);
        void RemoveField(string name);

        /// <summary>
        /// Add all public properties and fields from the dotnet type to the schema for this schema type
        /// </summary>
        /// <param name="autoCreateNewComplexTypes">If true (defaults to false) complex types (class) will be added to the schema</param>
        /// <param name="autoCreateEnumTypes">If true (default), automatically create ENUM types for enums found in the context object graph</param>
        /// <returns></returns>
        ISchemaType AddAllFields(bool autoCreateNewComplexTypes = false, bool autoCreateEnumTypes = true);
    }
}