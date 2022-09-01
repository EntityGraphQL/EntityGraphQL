using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema.Directives;

namespace EntityGraphQL.Schema
{
    public abstract class BaseSchemaTypeWithFields<TFieldType> : ISchemaType where TFieldType : IField
    {
        public ISchemaProvider Schema { get; }
        internal Dictionary<string, TFieldType> FieldsByName { get; } = new();
        public abstract Type TypeDotnet { get; }
        public string Name { get; }
        public string? Description { get; set; }
        public GqlTypeEnum GqlType { get; protected set; }

        protected List<ISchemaType> baseTypes = new();
        protected List<ISchemaType> possibleTypes = new();
        public IList<ISchemaType> BaseTypes => baseTypes.AsReadOnly();
        public IList<ISchemaType> PossibleTypes => possibleTypes.AsReadOnly();

        protected List<ISchemaDirective> directives = new();
        public IList<ISchemaDirective> Directives => directives.AsReadOnly();
        public bool IsInput { get { return GqlType == GqlTypeEnum.Input; } }
        public bool IsInterface { get { return GqlType == GqlTypeEnum.Interface; } }
        public bool IsEnum { get { return GqlType == GqlTypeEnum.Enum; } }
        public bool IsScalar { get { return GqlType == GqlTypeEnum.Scalar; } }

        public bool RequiresSelection => GqlType != GqlTypeEnum.Scalar && GqlType != GqlTypeEnum.Enum;
        public RequiredAuthorization? RequiredAuthorization { get; set; }
        private readonly Regex nameRegex = new("^[_a-zA-Z0-9]+$");

        public event Action<IField> OnAddField = delegate { };
        public event Action<object?> OnValidate = delegate { };

        protected BaseSchemaTypeWithFields(ISchemaProvider schema, string name, string? description, RequiredAuthorization? requiredAuthorization)
        {
            if (!nameRegex.IsMatch(name))
                throw new EntityGraphQLCompilerException($"Names must only contain [_a-zA-Z0-9] but '{name}' does not.");
            this.Schema = schema;
            Name = name;
            Description = description;
            RequiredAuthorization = requiredAuthorization;
        }

        public void ApplyAttributes(IEnumerable<Attribute> attributes)
        {
            if (attributes.Any())
            {
                foreach (var attribute in attributes)
                {
                    if (attribute is ExtensionAttribute extension)
                    {
                        extension.ApplyExtension(this);
                    }
                    else
                    {
                        //todo is this slow? can we get access to service provider?
                        var extensionAttribute = typeof(IExtensionAttribute<>).MakeGenericType(attribute.GetType());
                        var extensions = AppDomain.CurrentDomain.GetAssemblies()
                                .SelectMany(s => s.GetTypes())
                                .Where(p => extensionAttribute.IsAssignableFrom(p));

                        foreach (var extensionType in extensions)
                        {
                            var ext = Activator.CreateInstance(extensionType);
                            extensionAttribute.GetMethod("ApplyExtension", new[] { typeof(ISchemaType), attribute.GetType() })!.Invoke(ext, new object[] { this, attribute });
                        }

                    }
                }
            }
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

            OnAddField(field);

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

        public ISchemaType AddDirective(ISchemaDirective directive)
        {
            if (
                (GqlType == GqlTypeEnum.Scalar && !directive.On.Contains(TypeSystemDirectiveLocation.SCALAR)) ||
                (GqlType == GqlTypeEnum.Object && !directive.On.Contains(TypeSystemDirectiveLocation.OBJECT)) ||
                (GqlType == GqlTypeEnum.Interface && !directive.On.Contains(TypeSystemDirectiveLocation.INTERFACE)) ||
                (GqlType == GqlTypeEnum.Enum && !directive.On.Contains(TypeSystemDirectiveLocation.ENUM)) ||
                (GqlType == GqlTypeEnum.Input && !directive.On.Contains(TypeSystemDirectiveLocation.INPUT_OBJECT)) ||
                (GqlType == GqlTypeEnum.Union && !directive.On.Contains(TypeSystemDirectiveLocation.UNION))
            )
            {
                throw new EntityQuerySchemaException($"{TypeDotnet.Name} marked with {directive.GetType().Name} directive which is not valid on a {GqlType}");
            }

            directives.Add(directive);

            return this;
        }

        public void Validate(object? value)
        {
            OnValidate(value);
        }

        public abstract ISchemaType ImplementAllBaseTypes(bool addTypeIfNotInSchema = true, bool addAllFieldsOnAddedType = true);
        public abstract ISchemaType Implements<TClrType>(bool addTypeIfNotInSchema = true, bool addAllFieldsOnAddedType = true);
        public abstract ISchemaType Implements(string typeName);
    }
}
