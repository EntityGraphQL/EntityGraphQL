using System;
using System.Collections.Generic;

namespace EntityGraphQL.Schema
{
    public interface ISchemaType
    {
        Type ContextType { get; }
        string Name { get; }
        string Description { get; }
        bool IsInput { get; }

        Field GetField(string identifier);
        IEnumerable<Field> GetFields();
        bool HasField(string identifier);
        void AddFields(List<Field> fields);
        void AddField(Field field);
    }
}