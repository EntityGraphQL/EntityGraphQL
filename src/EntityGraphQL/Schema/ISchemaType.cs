using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace EntityGraphQL.Schema
{
    public interface ISchemaType
    {
        Type ContextType { get; }
        string Name { get; }
        string Description { get; }
        bool IsInput { get; }
        bool IsEnum { get; }

        Field GetField(string identifier, ClaimsIdentity claims);
        IEnumerable<Field> GetFields();
        bool HasField(string identifier);
        void AddFields(List<Field> fields);
        Field AddField(Field field);
        void RemoveField(string name);
        /// <summary>
        /// Add all public Properties and Fields from the DotNet type to the schema type.
        /// </summary>
        /// <param name="autoCreateNewComplexTypes">Default false. If true creates new schema types for any complex dotnet types found.false Also adding all it's fields</param>
        /// <param name="autoCreateEnumTypes">Default true. If true creates new Enum types in the schema for any enums found as field types</param>
        /// <typeparam name="TContextType"></typeparam>
        /// <returns></returns>
        ISchemaType AddAllFields(bool autoCreateNewComplexTypes = false, bool autoCreateEnumTypes = true);
    }
}