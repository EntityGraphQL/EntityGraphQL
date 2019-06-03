using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema
{
    public class SchemaType<TBaseType> : ISchemaType
    {
        public Type ContextType { get; protected set; }
        public string Name { get; protected set; }
        public bool IsInput { get; }

        public string Description => _description;

        private string _description;
        private Dictionary<string, Field> _fieldsByName = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
        private readonly Expression<Func<TBaseType, bool>> _filter;

        public SchemaType(string name, string description, Expression<Func<TBaseType, bool>> filter = null, bool isInput = false) : this(typeof(TBaseType), name, description, filter, isInput)
        {
        }

        public SchemaType(Type contextType, string name, string description, Expression<Func<TBaseType, bool>> filter = null, bool isInput = false)
        {
            ContextType = contextType;
            Name = name;
            _description = description;
            _filter = filter;
            IsInput = isInput;
            AddField("__typename", t => name, "Type name");
        }

        /// <summary>
        /// Add all public Properties and Fields from the base type
        /// </summary>
        public SchemaType<TBaseType> AddAllFields()
        {
            BuildFieldsFromBase(typeof(TBaseType));
            return this;
        }
        public void AddFields(List<Field> fields)
        {
            foreach (var f in fields)
            {
                AddField(f);
            }
        }
        public void AddField<TReturn>(Expression<Func<TBaseType, TReturn>> fieldSelection, string description, string returnSchemaType = null)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            AddField(exp.Member.Name, fieldSelection, description, returnSchemaType);
        }
        public void AddField(Field field)
        {
            if (_fieldsByName.ContainsKey(field.Name))
                throw new EntityQuerySchemaError($"Field {field.Name} already exists on type {this.Name}. Use ReplaceField() if this is intended.");

            _fieldsByName.Add(field.Name, field);
            if (!_fieldsByName.ContainsKey(field.Name))
                _fieldsByName.Add(field.Name, field);
        }
        public void AddField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> fieldSelection, string description, string returnSchemaType = null)
        {
            var field = new Field(name, fieldSelection, description, returnSchemaType);
            this.AddField(field);
        }
        public void ReplaceField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> selectionExpression, string description, string returnSchemaType = null)
        {
            var field = new Field(name, selectionExpression, description, returnSchemaType);
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
        public void AddField<TParams, TReturn>(string name, TParams argTypes, Expression<Func<TBaseType, TParams, TReturn>> selectionExpression, string description, string returnSchemaType = null)
        {
            var field = new Field(name, selectionExpression, description, returnSchemaType, argTypes);
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

        /// <summary>
        /// Checks for a field by name only. There could be multiple fields with the same name but different arguments (overloads)
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public bool HasFieldByNameOnly(string identifier)
        {
            return _fieldsByName.ContainsKey(identifier);
        }

        private void BuildFieldsFromBase(Type contextType)
        {
            foreach (var f in ContextType.GetProperties())
            {
                if (!_fieldsByName.ContainsKey(f.Name))
                {
                    //Get Description from ComponentModel.DescriptionAttribute
                    string description = string.Empty;
                    var d = (System.ComponentModel.DescriptionAttribute)f.GetCustomAttribute(typeof(System.ComponentModel.DescriptionAttribute), false);
                    if (d != null)
                        description = d.Description;

                    var parameter = Expression.Parameter(ContextType);
                    this.AddField(new Field(f.Name, Expression.Lambda(Expression.Property(parameter, f.Name), parameter), description, null));
                }
            }
            foreach (var f in ContextType.GetFields())
            {
                if (!_fieldsByName.ContainsKey(f.Name))
                {
                    //Get Description from ComponentModel.DescriptionAttribute
                    string description = string.Empty;
                    var d = (System.ComponentModel.DescriptionAttribute)f.GetCustomAttribute(typeof(System.ComponentModel.DescriptionAttribute), false);
                    if (d != null)
                        description = d.Description;

                    var parameter = Expression.Parameter(ContextType);
                    this.AddField(new Field(f.Name, Expression.Lambda(Expression.Field(parameter, f.Name), parameter), description, null));
                }
            }
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

        public void RemoveField(string name, params string[] arguments)
        {
            if (_fieldsByName.ContainsKey(name))
            {
                _fieldsByName.Remove(name);
            }
        }
        public void RemoveField(Expression<Func<TBaseType, object>> fieldSelection)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            RemoveField(exp.Member.Name);
        }
    }
}