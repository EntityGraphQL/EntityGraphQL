using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Schema
{
    public abstract class BaseSchemaTypeWithFields<TFieldType> : ISchemaType where TFieldType : IField
    {
        internal ISchemaProvider Schema { get; }
        internal Dictionary<string, TFieldType> FieldsByName { get; } = new();
        public abstract Type TypeDotnet { get; }
        public string Name { get; }
        public string? Description { get; set; }
        public GqlTypeEnum GqlType { get; protected set; }

        protected List<ISchemaType> baseTypes = new();
        public IList<ISchemaType> BaseTypes => baseTypes.AsReadOnly();

        public abstract bool IsOneOf { get; }
        public bool IsInput { get { return GqlType == GqlTypeEnum.Input; } }
        public bool IsInterface { get { return GqlType == GqlTypeEnum.Interface; } }
        public bool IsEnum { get { return GqlType == GqlTypeEnum.Enum; } }
        public bool IsScalar { get { return GqlType == GqlTypeEnum.Scalar; } }

        public bool RequiresSelection => GqlType != GqlTypeEnum.Scalar && GqlType != GqlTypeEnum.Enum;
        public RequiredAuthorization? RequiredAuthorization { get; set; }
        private readonly Regex nameRegex = new("^[_a-zA-Z0-9]+$");

        protected BaseSchemaTypeWithFields(ISchemaProvider schema, string name, string? description, RequiredAuthorization? requiredAuthorization)
        {
            if (!nameRegex.IsMatch(name))
                throw new EntityGraphQLCompilerException($"Names must only contain [_a-zA-Z0-9] but '{name}' does not.");
            this.Schema = schema;
            Name = name;
            Description = description;
            RequiredAuthorization = requiredAuthorization;
        }

        /// <summary>
        /// Search for a field by name. Use HasField() to check if field exists.
        /// </summary>
        /// <param name="identifier">Field name. Case sensitive</param>
        /// <param name="requestContext">Current request context. Used by EntityGraphQL when compiling queries. If are calling this during schema configure, you can pass null</param>
        /// <returns>The field object for further configuration</returns>
        /// <exception cref="EntityGraphQLAccessException"></exception>
        /// <exception cref="EntityGraphQLCompilerException">If field if not found</exception>
        public IField GetField(string identifier, QueryRequestContext? requestContext)
        {
            if (FieldsByName.ContainsKey(identifier))
            {
                var field = FieldsByName[identifier];
                if (requestContext != null && requestContext.AuthorizationService != null && !requestContext.AuthorizationService.IsAuthorized(requestContext.User, field.RequiredAuthorization))
                    throw new EntityGraphQLAccessException($"You are not authorized to access the '{identifier}' field on type '{Name}'.");
                if (requestContext != null && requestContext.AuthorizationService != null && !requestContext.AuthorizationService.IsAuthorized(requestContext.User, field.ReturnType.SchemaType.RequiredAuthorization))
                    throw new EntityGraphQLAccessException($"You are not authorized to access the '{field.ReturnType.SchemaType.Name}' type returned by field '{identifier}'.");

                return FieldsByName[identifier];
            }

            throw new EntityGraphQLCompilerException($"Field '{identifier}' not found on type '{Name}'");
        }
        /// <summary>
        /// Return all the fields defined on this type
        /// </summary>
        /// <returns>List of Field objects</returns>
        public IEnumerable<IField> GetFields()
        {
            return FieldsByName.Values.Cast<IField>();
        }
        /// <summary>
        /// Checks if this type has a field with the given name
        /// </summary>
        /// <param name="identifier">Field name. Case sensitive</param>
        /// <returns></returns>
        public bool HasField(string identifier, QueryRequestContext? requestContext)
        {
            if (FieldsByName.ContainsKey(identifier))
            {
                var field = FieldsByName[identifier];
                if (requestContext != null && requestContext.AuthorizationService != null && !requestContext.AuthorizationService.IsAuthorized(requestContext.User, field.RequiredAuthorization))
                    return false;

                return true;
            }

            return false;
        }
        public abstract ISchemaType AddAllFields(SchemaBuilderOptions? options = null);

        public void AddFields(IEnumerable<IField> fields)
        {
            foreach (var f in fields)
            {
                AddField(f);
            }
        }

        public IField AddField(IField field)
        {
            if (FieldsByName.ContainsKey(field.Name))
                throw new EntityQuerySchemaException($"Field {field.Name} already exists on type {this.Name}. Use ReplaceField() if this is intended.");

            if (IsOneOf && field.ReturnType.TypeNotNullable)
                throw new EntityQuerySchemaException($"{TypeDotnet.Name} is a OneOf type but all its fields are not nullable. OneOf input types require all the field to be nullable.");


            FieldsByName.Add(field.Name, (TFieldType)field);
            return field;
        }

        /// <summary>
        /// Remove a field by the given name. Case sensitive. If the field does not exist, nothing happens.
        /// </summary>
        /// <param name="name"></param>
        public void RemoveField(string name)
        {
            FieldsByName.Remove(name);
        }

        public abstract ISchemaType ImplementAllBaseTypes(bool addTypeIfNotInSchema = true, bool addAllFieldsOnAddedType = true);
        public abstract ISchemaType Implements<TClrType>(bool addTypeIfNotInSchema = true, bool addAllFieldsOnAddedType = true);
        public abstract ISchemaType Implements(string typeName);
    }
}
