using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema
{
    public class SchemaType<TBaseType> : ISchemaType
    {
        private readonly ISchemaProvider schema;
        public Type ContextType { get; protected set; }
        public string Name { get; internal set; }
        public bool IsInput { get; }
        public bool IsEnum { get; }
        public bool IsScalar { get; }

        public string Description { get; internal set; }

        private readonly Dictionary<string, Field> _fieldsByName = new Dictionary<string, Field>();

        internal SchemaType(ISchemaProvider schema, string name, string description, bool isInput = false, bool isEnum = false, bool isScalar = false) : this(schema, typeof(TBaseType), name, description, isInput, isEnum, isScalar)
        {
        }

        internal SchemaType(ISchemaProvider schema, Type contextType, string name, string description, bool isInput = false, bool isEnum = false, bool isScalar = false)
        {
            this.schema = schema;
            ContextType = contextType;
            Name = name;
            Description = description;
            IsInput = isInput;
            IsEnum = isEnum;
            IsScalar = isScalar;
            if (!isScalar)
                AddField("__typename", t => name, "Type name", null, false, null);
        }

        public ISchemaType AddAllFields(bool autoCreateNewComplexTypes = false, bool autoCreateEnumTypes = true, Func<MemberInfo, string> fieldNamer = null)
        {
            if (IsEnum)
            {
                foreach (var field in ContextType.GetTypeInfo().GetFields())
                {
                    if (field.Name == "value__")
                        continue;

                    var enumName = Enum.Parse(ContextType, field.Name).ToString();
                    var description = (field.GetCustomAttribute(typeof(DescriptionAttribute)) as DescriptionAttribute)?.Description;
                    AddField(new Field(enumName, null, description, new GqlTypeInfo(() => schema.Type(ContextType), ContextType), null));
                }
            }
            else
            {
                var fields = SchemaBuilder.GetFieldsFromObject(ContextType, schema, autoCreateEnumTypes, fieldNamer, autoCreateNewComplexTypes);
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
        public Field AddField<TReturn>(Expression<Func<TBaseType, TReturn>> fieldSelection, string description, string returnSchemaType = null, bool? isNullable = null, RequiredClaims authorizeClaims = null)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            return AddField(SchemaGenerator.ToCamelCaseStartsLower(exp.Member.Name), fieldSelection, description, returnSchemaType, isNullable, authorizeClaims);
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
        public Field AddField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> fieldSelection, string description, string returnSchemaType = null, bool? isNullable = null, RequiredClaims authorizeClaims = null)
        {
            var field = new Field(name, fieldSelection, description, new GqlTypeInfo(!string.IsNullOrEmpty(returnSchemaType) ? (Func<ISchemaType>)(() => schema.Type(returnSchemaType)) : () => schema.Type(typeof(TReturn).GetNonNullableOrEnumerableType()), typeof(TReturn)), authorizeClaims);
            if (isNullable.HasValue)
                field.ReturnType.TypeNotNullable = !isNullable.Value;
            this.AddField(field);
            return field;
        }
        public Field AddField<TService, TReturn>(string name, Expression<Func<TBaseType, TService, TReturn>> fieldSelection, string description, string returnSchemaType = null, bool? isNullable = null, RequiredClaims authorizeClaims = null)
        {
            var field = new Field(schema, name, fieldSelection, description, null, new GqlTypeInfo(!string.IsNullOrEmpty(returnSchemaType) ? (Func<ISchemaType>)(() => schema.Type(returnSchemaType)) : () => schema.Type(typeof(TReturn).GetNonNullableOrEnumerableType()), typeof(TReturn)), authorizeClaims);
            if (isNullable.HasValue)
                field.ReturnType.TypeNotNullable = !isNullable.Value;
            this.AddField(field);
            return field;
        }
        public Field ReplaceField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> selectionExpression, string description, string returnSchemaType = null, bool? isNullable = null, RequiredClaims authorizeClaims = null)
        {
            var field = new Field(name, selectionExpression, description, new GqlTypeInfo(!string.IsNullOrEmpty(returnSchemaType) ? (Func<ISchemaType>)(() => schema.Type(returnSchemaType)) : () => schema.Type(typeof(TReturn).GetNonNullableOrEnumerableType()), typeof(TReturn)), authorizeClaims);
            if (isNullable.HasValue)
                field.ReturnType.TypeNotNullable = !isNullable.Value;
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
        public Field AddField<TParams, TReturn>(string name, TParams argTypes, Expression<Func<TBaseType, TParams, TReturn>> selectionExpression, string description, string returnSchemaType = null, bool? isNullable = null, RequiredClaims authorizeClaims = null)
        {
            var field = new Field(schema, name, selectionExpression, description, argTypes, new GqlTypeInfo(!string.IsNullOrEmpty(returnSchemaType) ? (Func<ISchemaType>)(() => schema.Type(returnSchemaType)) : () => schema.Type(typeof(TReturn).GetNonNullableOrEnumerableType()), typeof(TReturn)), authorizeClaims);
            if (isNullable.HasValue)
                field.ReturnType.TypeNotNullable = !isNullable.Value;
            this.AddField(field);
            return field;
        }
        public Field AddField<TParams, TService, TReturn>(string name, TParams argTypes, Expression<Func<TBaseType, TParams, TService, TReturn>> selectionExpression, string description, string returnSchemaType = null, bool? isNullable = null, RequiredClaims authorizeClaims = null)
        {
            var field = new Field(schema, name, selectionExpression, description, argTypes, new GqlTypeInfo(!string.IsNullOrEmpty(returnSchemaType) ? (Func<ISchemaType>)(() => schema.Type(returnSchemaType)) : () => schema.Type(typeof(TReturn).GetNonNullableOrEnumerableType()), typeof(TReturn)), authorizeClaims);
            if (isNullable.HasValue)
                field.ReturnType.TypeNotNullable = !isNullable.Value;
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
        public void ReplaceField<TParams, TReturn>(string name, TParams argTypes, Expression<Func<TBaseType, TParams, TReturn>> selectionExpression, string description, string returnSchemaType = null, RequiredClaims authorizeClaims = null)
        {
            var field = new Field(schema, name, selectionExpression, description, argTypes, new GqlTypeInfo(!string.IsNullOrEmpty(returnSchemaType) ? (Func<ISchemaType>)(() => schema.Type(returnSchemaType)) : () => schema.Type(typeof(TReturn).GetNonNullableOrEnumerableType()), typeof(TReturn)), authorizeClaims);
            _fieldsByName[field.Name] = field;
        }

        public Field GetField(string identifier, ClaimsIdentity claims = null)
        {
            if (_fieldsByName.ContainsKey(identifier))
            {
                var field = _fieldsByName[identifier];
                if (!AuthUtil.IsAuthorized(claims, field.AuthorizeClaims))
                {
                    throw new EntityGraphQLAccessException($"You do not have access to field '{identifier}' on type '{Name}'. You require any of the following security claims [{string.Join(", ", field.AuthorizeClaims.Claims.SelectMany(r => r))}]");
                }
                return _fieldsByName[identifier];
            }

            throw new EntityGraphQLCompilerException($"Field {identifier} not found");
        }
        
        /// <summary>
        /// Get a field by an expression selection on the real type. The name is changed to lowerCaseCamel
        /// </summary>
        /// <param name="fieldSelection"></param>
        public Field GetField(Expression<Func<TBaseType, object>> fieldSelection, ClaimsIdentity claims = null)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            return GetField(SchemaGenerator.ToCamelCaseStartsLower(exp.Member.Name), claims);
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
            RemoveField(SchemaGenerator.ToCamelCaseStartsLower(exp.Member.Name));
        }
    }
}
