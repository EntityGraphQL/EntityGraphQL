using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Schema
{
    public class SchemaType<TBaseType> : ISchemaType
    {
        public Type ContextType { get; protected set; }
        public string Name { get; internal set; }
        public bool IsInput { get; }
        public bool IsEnum { get; }

        public string Description { get; internal set; }

        private readonly Dictionary<string, Field> _fieldsByName = new Dictionary<string, Field>();

        public SchemaType(string name, string description, bool isInput = false, bool isEnum = false) : this(typeof(TBaseType), name, description, isInput, isEnum)
        {
        }

        public SchemaType(Type contextType, string name, string description, bool isInput = false, bool isEnum = false)
        {
            ContextType = contextType;
            Name = name;
            Description = description;
            IsInput = isInput;
            IsEnum = isEnum;
            AddField("__typename", t => name, "Type name");
        }

        public ISchemaType AddAllFields<TContextType>(MappedSchemaProvider<TContextType> schema, bool autoCreateNewComplexTypes = false, bool autoCreateEnumTypes = true)
        {
            if (IsEnum)
            {
                foreach (var item in Enum.GetNames(ContextType))
                {
                    AddField(new Field(item, null, "", Name, ContextType));
                }
            }
            else
            {
                var fields = SchemaBuilder.GetFieldsFromObject(ContextType, schema, autoCreateEnumTypes, autoCreateNewComplexTypes);
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
        public void AddField<TReturn>(Expression<Func<TBaseType, TReturn>> fieldSelection, string description, string returnSchemaType = null, bool? isNullable = null)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            AddField(SchemaGenerator.ToCamelCaseStartsLower(exp.Member.Name), fieldSelection, description, returnSchemaType, isNullable);
        }
        public void AddField(Field field)
        {
            if (_fieldsByName.ContainsKey(field.Name))
                throw new EntityQuerySchemaException($"Field {field.Name} already exists on type {this.Name}. Use ReplaceField() if this is intended.");

            _fieldsByName.Add(field.Name, field);
            if (!_fieldsByName.ContainsKey(field.Name))
                _fieldsByName.Add(field.Name, field);
        }
        public void AddField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> fieldSelection, string description, string returnSchemaType = null, bool? isNullable = null)
        {
            var field = new Field(name, fieldSelection, description, returnSchemaType);
            if (isNullable.HasValue)
                field.ReturnTypeNotNullable = !isNullable.Value;
            this.AddField(field);
        }
        public void ReplaceField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> selectionExpression, string description, string returnSchemaType = null, bool? isNullable = null)
        {
            var field = new Field(name, selectionExpression, description, returnSchemaType);
            if (isNullable.HasValue)
                field.ReturnTypeNotNullable = !isNullable.Value;
            _fieldsByName[field.Name] = field;
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
        public void AddField<TParams, TReturn>(string name, TParams argTypes, Expression<Func<TBaseType, TParams, TReturn>> selectionExpression, string description, string returnSchemaType = null, bool? isNullable = null)
        {
            var field = new Field(name, selectionExpression, description, returnSchemaType, argTypes);
            if (isNullable.HasValue)
                field.ReturnTypeNotNullable = !isNullable.Value;
            this.AddField(field);
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
            var field = new Field(name, selectionExpression, description, returnSchemaType, argTypes);
            _fieldsByName[field.Name] = field;
        }

        public Field GetField(string identifier)
        {
            if (_fieldsByName.ContainsKey(identifier))
                return _fieldsByName[identifier];

            throw new EntityGraphQLCompilerException($"Field {identifier} not found");
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