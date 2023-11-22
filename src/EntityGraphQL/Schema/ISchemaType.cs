using System;
using System.Collections.Generic;
using EntityGraphQL.Schema.Directives;

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
        GqlTypes GqlType { get; }
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

        ISchemaProvider Schema { get; }
        /// <summary>
        /// If the type in a query requires a selection {  }
        /// </summary>
        bool RequiresSelection { get; }
        IList<ISchemaType> BaseTypesReadOnly { get; }
        IList<ISchemaType> PossibleTypesReadOnly { get; }

        IList<ISchemaDirective> Directives { get; }
        ISchemaType AddDirective(ISchemaDirective directive);
        void ApplyAttributes(IEnumerable<Attribute> attributes);
        RequiredAuthorization? RequiredAuthorization { get; set; }
        IField GetField(string identifier, QueryRequestContext? requestContext);
        IEnumerable<IField> GetFields();
        bool HasField(string identifier, QueryRequestContext? requestContext);
        ISchemaType AddAllFields(SchemaBuilderOptions? options = null);
        void AddFields(IEnumerable<IField> fields);
        IField AddField(IField field);
        void RemoveField(string name);
        /// <summary>
        /// Searches the dotnet type for any interfaces or base type and marks this schema type as implementing those interfaces in the schema.
        /// </summary>
        /// <param name="addTypeIfNotInSchema">If true and the TClrType type is not already in the schema it will be added as an interface. If the type is in the schema it must be an interface</param>
        /// <param name="addAllFieldsOnAddedType">If true and addTypeIfNotInSchema = true and the type is added by this method (was not 
        /// <returns></returns>
        ISchemaType ImplementAllBaseTypes(bool addTypeIfNotInSchema = true, bool addAllFieldsOnAddedType = true);
        /// <summary>
        /// Tells the schema that this type implements another type of TClrType.
        /// </summary>
        /// <typeparam name="TClrType">The dotnet type this schema type implements</typeparam>
        /// <param name="addTypeIfNotInSchema">If true and the TClrType type is not already in the schema it will be added as an interface. If the type is in the schema it must be an interface</param>
        /// <param name="addAllFieldsOnAddedType">If true and addTypeIfNotInSchema = true and the type is added by this method (was not 
        /// in the schema before), all the fields on the implemented type will be added to the schema. e.g. .AddAllFields() is called on 
        /// the added type</param>
        /// <returns></returns>
#pragma warning disable CA1716
        ISchemaType Implements<TClrType>(bool addTypeIfNotInSchema = true, bool addAllFieldsOnAddedType = true);
        /// <summary>
        /// Tells the schema that this type implements another type of typeName. typeName needs to be an interface type existing in the schema
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        ISchemaType Implements(string typeName);
#pragma warning restore CA1716
        void Validate(object? value);

        public event Action<IField> OnAddField;
        public event Action<object?> OnValidate;
    }
}