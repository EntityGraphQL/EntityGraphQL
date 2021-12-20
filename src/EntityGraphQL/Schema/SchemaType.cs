using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using Microsoft.AspNetCore.Authorization;

namespace EntityGraphQL.Schema
{
    public class SchemaType<TBaseType> : ISchemaType
    {
        private readonly ISchemaProvider schema;
        public Type TypeDotnet { get; protected set; }
        public string Name { get; internal set; }
        public bool IsInput { get; }
        public bool IsEnum { get; }
        public bool IsScalar { get; }
        public RequiredAuthorization RequiredAuthorization { get; set; }

        private readonly Func<string, string> fieldNamer;

        public string Description { get; internal set; }

        private readonly Dictionary<string, Field> _fieldsByName = new();

        public SchemaType(ISchemaProvider schema, string name, string description, RequiredAuthorization requiredAuthorization, Func<string, string> fieldNamer, bool isInput = false, bool isEnum = false, bool isScalar = false)
            : this(schema, typeof(TBaseType), name, description, requiredAuthorization, fieldNamer, isInput, isEnum, isScalar)
        {
        }

        public SchemaType(ISchemaProvider schema, Type dotnetType, string name, string description, RequiredAuthorization requiredAuthorization, Func<string, string> fieldNamer, bool isInput = false, bool isEnum = false, bool isScalar = false)
        {
            this.schema = schema;
            TypeDotnet = dotnetType;
            Name = name;
            Description = description;
            IsInput = isInput;
            IsEnum = isEnum;
            IsScalar = isScalar;
            RequiredAuthorization = requiredAuthorization;
            this.fieldNamer = fieldNamer;
            if (!isScalar)
                AddField("__typename", t => name, "Type name", null).IsNullable(false);
        }

        public ISchemaType AddAllFields(bool autoCreateNewComplexTypes = false, bool autoCreateEnumTypes = true)
        {
            if (IsEnum)
            {
                foreach (var field in TypeDotnet.GetTypeInfo().GetFields())
                {
                    if (field.Name == "value__")
                        continue;

                    var enumName = Enum.Parse(TypeDotnet, field.Name).ToString();
                    var description = (field.GetCustomAttribute(typeof(DescriptionAttribute)) as DescriptionAttribute)?.Description;
                    AddField(new Field(schema, enumName, null, description, new GqlTypeInfo(() => schema.Type(TypeDotnet), TypeDotnet), RequiredAuthorization.GetRequiredAuthFromField(field), fieldNamer));
                }
            }
            else
            {
                var fields = SchemaBuilder.GetFieldsFromObject(TypeDotnet, schema, autoCreateEnumTypes, schema.SchemaFieldNamer, autoCreateNewComplexTypes);
                AddFields(fields);
            }
            return this;
        }
        public void AddFields(List<Field> fields)
        {
            foreach (var f in fields)
            {
                AddField(f);
            }
        }
        /// <summary>
        /// Add a field from a type expression. The name to converted to lowerCamelCase
        /// </summary>
        /// <param name="fieldSelection"></param>
        /// <param name="description"></param>
        /// <param name="returnSchemaType"></param>
        /// <typeparam name="TReturn"></typeparam>
        public Field AddField<TReturn>(Expression<Func<TBaseType, TReturn>> fieldSelection, string description, string returnSchemaType = null)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            return AddField(schema.SchemaFieldNamer(exp.Member.Name), fieldSelection, description, returnSchemaType);
        }

        public Field AddField(Field field)
        {
            if (_fieldsByName.ContainsKey(field.Name))
                throw new EntityQuerySchemaException($"Field {field.Name} already exists on type {this.Name}. Use ReplaceField() if this is intended.");

            _fieldsByName.Add(field.Name, field);
            if (!_fieldsByName.ContainsKey(field.Name))
                _fieldsByName.Add(field.Name, field);
            return field;
        }
        public Field AddField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> fieldSelection, string description, string returnSchemaType = null)
        {
            var requiredAuth = RequiredAuthorization.GetRequiredAuthFromExpression(fieldSelection);

            var field = new Field(schema, name, fieldSelection, description, SchemaBuilder.MakeGraphQlType(schema, typeof(TReturn), returnSchemaType), requiredAuth, fieldNamer);
            this.AddField(field);
            return field;
        }

        public Field AddField<TService, TReturn>(string name, Expression<Func<TBaseType, TService, TReturn>> fieldSelection, string description, string returnSchemaType = null)
        {
            var requiredAuth = RequiredAuthorization.GetRequiredAuthFromExpression(fieldSelection);

            var field = new Field(schema, name, fieldSelection, description, null, SchemaBuilder.MakeGraphQlType(schema, typeof(TReturn), returnSchemaType), requiredAuth, fieldNamer);
            this.AddField(field);
            return field;
        }
        public Field ReplaceField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> selectionExpression, string description, string returnSchemaType = null)
        {
            var requiredAuth = RequiredAuthorization.GetRequiredAuthFromExpression(selectionExpression);

            var field = new Field(schema, name, selectionExpression, description, SchemaBuilder.MakeGraphQlType(schema, typeof(TReturn), returnSchemaType), requiredAuth, fieldNamer);
            _fieldsByName[field.Name] = field;
            return field;
        }

        /// <summary>
        /// Add a field with arguments.
        ///     field(arg: val)
        /// </summary>
        /// <param name="name">Field name</param>
        /// <param name="argTypes">Anonymous object defines the names and types of each argument</param>
        /// <param name="selectionExpression">The expression that selects the data from TBaseType using the arguments</param>
        /// <param name="returnSchemaType">The schema type to return, it defines the fields available on the return object. If null, defaults to TReturn type mapped in the schema.</param>
        /// <typeparam name="TParams">Type describing the arguments</typeparam>
        /// <typeparam name="TReturn">The return entity type that is mapped to a type in the schema</typeparam>
        /// <returns></returns>
        public Field AddField<TParams, TReturn>(string name, TParams argTypes, Expression<Func<TBaseType, TParams, TReturn>> selectionExpression, string description, string returnSchemaType = null)
        {
            var requiredAuth = RequiredAuthorization.GetRequiredAuthFromExpression(selectionExpression);

            var field = new Field(schema, name, selectionExpression, description, argTypes, SchemaBuilder.MakeGraphQlType(schema, typeof(TReturn), returnSchemaType), requiredAuth, fieldNamer);
            this.AddField(field);
            return field;
        }
        public Field AddField<TParams, TService, TReturn>(string name, TParams argTypes, Expression<Func<TBaseType, TParams, TService, TReturn>> selectionExpression, string description, string returnSchemaType = null)
        {
            var requiredAuth = RequiredAuthorization.GetRequiredAuthFromExpression(selectionExpression);

            var field = new Field(schema, name, selectionExpression, description, argTypes, SchemaBuilder.MakeGraphQlType(schema, typeof(TReturn), returnSchemaType), requiredAuth, fieldNamer);
            this.AddField(field);
            return field;
        }

        /// <summary>
        /// Replaces a field by Name and Argument Names (that is the key)
        /// </summary>
        /// <param name="name"></param>
        /// <param name="argTypes"></param>
        /// <param name="selectionExpression"></param>
        /// <param name="description"></param>
        /// <param name="returnSchemaType"></param>
        /// <typeparam name="TParams"></typeparam>
        /// <typeparam name="TReturn"></typeparam>
        /// <returns></returns>
        public void ReplaceField<TParams, TReturn>(string name, TParams argTypes, Expression<Func<TBaseType, TParams, TReturn>> selectionExpression, string description, string returnSchemaType = null)
        {
            var requiredAuth = RequiredAuthorization.GetRequiredAuthFromExpression(selectionExpression);

            var field = new Field(schema, name, selectionExpression, description, argTypes, SchemaBuilder.MakeGraphQlType(schema, typeof(TReturn), returnSchemaType), requiredAuth, fieldNamer);
            _fieldsByName[field.Name] = field;
        }

        public Field GetField(string identifier, UserAuthInfo authInfo)
        {
            if (_fieldsByName.ContainsKey(identifier))
            {
                var field = _fieldsByName[identifier];
                if (authInfo != null && !authInfo.IsAuthorized(field.RequiredAuthorization))
                {
                    throw new EntityGraphQLAccessException($"You are not authorized to access the '{identifier}' field on type '{Name}'.");
                }
                return _fieldsByName[identifier];
            }

            throw new EntityGraphQLCompilerException($"Field {identifier} not found");
        }

        /// <summary>
        /// Get a field by an expression selection on the real type. The name is changed to lowerCaseCamel
        /// </summary>
        /// <param name="fieldSelection"></param>
        public Field GetField(Expression<Func<TBaseType, object>> fieldSelection, UserAuthInfo authInfo)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            return GetField(schema.SchemaFieldNamer(exp.Member.Name), authInfo);
        }

        public IEnumerable<Field> GetFields()
        {
            return _fieldsByName.Values;
        }
        /// <summary>
        /// Checks if type has a field with the given name and the given arguments
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public bool HasField(string identifier)
        {
            return _fieldsByName.ContainsKey(identifier);
        }

        public void RemoveField(string name)
        {
            if (_fieldsByName.ContainsKey(name))
            {
                _fieldsByName.Remove(name);
            }
        }
        /// <summary>
        /// Remove a field by an expression selection on the real type. The name is changed to lowerCaseCamel
        /// </summary>
        /// <param name="fieldSelection"></param>
        public void RemoveField(Expression<Func<TBaseType, object>> fieldSelection)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            RemoveField(schema.SchemaFieldNamer(exp.Member.Name));
        }

        /// <summary>
        /// To access this type all claims listed here are required
        /// </summary>
        /// <param name="claims"></param>
        [Obsolete("Use RequiresAllRoles")]
        public SchemaType<TBaseType> RequiresAllClaims(params string[] claims)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAllRoles(claims);
            return this;
        }
        /// <summary>
        /// To access this type any of the claims listed is required
        /// </summary>
        /// <param name="claims"></param>
        [Obsolete("Use RequiresAnyRole")]
        public SchemaType<TBaseType> RequiresAnyClaim(params string[] claims)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAnyRole(claims);
            return this;
        }
        /// <summary>
        /// To access this type all roles listed here are required
        /// </summary>
        /// <param name="roles"></param>
        public SchemaType<TBaseType> RequiresAllRoles(params string[] roles)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAllRoles(roles);
            return this;
        }
        /// <summary>
        /// To access this type any of the roles listed is required
        /// </summary>
        /// <param name="roles"></param>
        public SchemaType<TBaseType> RequiresAnyRole(params string[] roles)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAnyRole(roles);
            return this;
        }
        /// <summary>
        /// To access this type all policies listed here are required
        /// </summary>
        /// <param name="policies"></param>
        public SchemaType<TBaseType> RequiresAllPolicies(params string[] policies)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAllPolicies(policies);
            return this;
        }
        /// <summary>
        /// To access this type any of the policies listed is required
        /// </summary>
        /// <param name="policies"></param>
        public SchemaType<TBaseType> RequiresAnyPolicy(params string[] policies)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAnyPolicy(policies);
            return this;
        }
    }
}
