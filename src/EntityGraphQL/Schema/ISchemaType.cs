using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityGraphQL.Schema
{
    public interface ISchemaType
    {
        Type ContextType { get; }
        string Name { get; }
        string Description { get; }
        bool IsInput { get; }
        bool IsEnum { get; }

        Field GetField(string identifier);
        IEnumerable<Field> GetFields();
        bool HasField(string identifier);
        void AddFields(List<Field> fields);
        void AddField(Field field);
        void RemoveField(string name);
        /// <summary>
        /// Add all fields and properties from the dotnet type to the schema type
        /// </summary>
        /// <returns></returns>
        ISchemaType AddAllFields();
    }
}