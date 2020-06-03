using System;
using System.Collections.Generic;
using System.Reflection;
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
        bool IsScalar { get; }

        Field GetField(string identifier, ClaimsIdentity claims);
        IEnumerable<Field> GetFields();
        bool HasField(string identifier);
        void AddFields(List<Field> fields);
        Field AddField(Field field);
        void RemoveField(string name);

        /// <summary>
        /// Add all public properties and fields from the dotnet type to the schema for this schema type
        /// </summary>
        /// <param name="autoCreateIdArguments">If true (default), automatically create a field for any root array thats context object contains an Id property. I.e. If Actor has an Id property and the root TContextType contains IEnumerable<Actor> Actors. A root field Actor(id) will be created.</param>
        /// <param name="autoCreateIdArguments">If true (default), automatically create ENUM types for enums found in the context object graph</param>
        /// <param name="fieldNamer">Optionally provider a function to generate the GraphQL field name. By default this will make fields names that follow GQL style in lowerCaseCamelStyle</param>
        /// <returns></returns>
        ISchemaType AddAllFields(bool autoCreateNewComplexTypes = false, bool autoCreateEnumTypes = true, Func<MemberInfo, string> fieldNamer = null);
    }
}