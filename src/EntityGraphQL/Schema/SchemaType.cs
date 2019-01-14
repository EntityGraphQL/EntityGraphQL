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
    public interface ISchemaType
    {
        Type ContextType { get; }
        string Name { get; }
        bool IsInput { get; }

        Field GetField(string identifier, params string[] arguments);
        IEnumerable<Field> GetFields();
        bool HasField(string identifier, params string[] arguments);
        void AddFields(List<Field> fields);
        void AddField(Field field);
        bool HasFieldByNameOnly(string identifier);
        IEnumerable<Field> GetFieldsByNameOnly(string identifier);
    }

    public class SchemaType<TBaseType> : ISchemaType
    {
        public Type ContextType { get; protected set; }
        public string Name { get; protected set; }
        public bool IsInput { get; }

        private string _description;
        private Dictionary<string, Field> _fieldsByKey = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<Field>> _fieldsByName = new Dictionary<string, List<Field>>(StringComparer.OrdinalIgnoreCase);
        private readonly Expression<Func<TBaseType, bool>> _filter;

        public SchemaType(string name, string description, Expression<Func<TBaseType, bool>> filter = null, bool isInput = false)
        {
            ContextType = typeof(TBaseType);
            Name = name;
            _description = description;
            _filter = filter;
            IsInput = isInput;
            AddField("__typename", t => name, "Type name");
        }

        /// <summary>
        /// Add all public Properties and Fields from the base type
        /// </summary>
        public void AddAllFields()
        {
            BuildFieldsFromBase(typeof(TBaseType));
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
            if (_fieldsByKey.ContainsKey(field.Key))
                throw new EntityQuerySchemaError($"Field {field.Name} already exists on type {this.Name} with the same argument names. Use ReplaceField() if this is intended.");

            _fieldsByKey.Add(field.Key, field);
            if (!_fieldsByName.ContainsKey(field.Name))
                _fieldsByName.Add(field.Name, new List<Field>());
            _fieldsByName[field.Name].Add(field);
        }
        public void AddField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> fieldSelection, string description, string returnSchemaType = null)
        {
            var field = new Field(name, fieldSelection, description, returnSchemaType);
            this.AddField(field);
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
            var oldField = _fieldsByKey.ContainsKey(field.Key) ? _fieldsByKey[field.Key] : null;
            _fieldsByKey[field.Key] = field;
            if (oldField != null && _fieldsByName.ContainsKey(field.Name))
            {
                _fieldsByName[field.Name].Remove(oldField);
            }
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

        public IEnumerable<Field> GetFieldsByNameOnly(string identifier)
        {
            return _fieldsByName[identifier];
        }

        private void BuildFieldsFromBase(Type contextType)
        {
            foreach (var f in ContextType.GetProperties())
            {
                if (!_fieldsByKey.ContainsKey(f.Name))
                {
                    var parameter = Expression.Parameter(ContextType);
                    this.AddField(new Field(f.Name, Expression.Lambda(Expression.Property(parameter, f.Name), parameter), string.Empty, string.Empty));
                }
            }
            foreach (var f in ContextType.GetFields())
            {
                if (!_fieldsByKey.ContainsKey(f.Name))
                {
                    var parameter = Expression.Parameter(ContextType);
                    this.AddField(new Field(f.Name, Expression.Lambda(Expression.Field(parameter, f.Name), parameter), string.Empty, string.Empty));
                }
            }
        }

        public Field GetField(string identifier, params string[] arguments)
        {
            var key = Field.MakeFieldKey(identifier, arguments);
            if (_fieldsByKey.ContainsKey(key))
                return _fieldsByKey[key];
            // they could be looking for a field that has default argument values
            if (_fieldsByName.ContainsKey(identifier))
            {
                var probableFields = _fieldsByName[identifier].Where(f => f.RequiredArgumentNames.All(r => arguments.Contains(r)));
                if (probableFields.Count() > 1)
                {
                    throw new EntityGraphQLCompilerException($"Field {identifier}({string.Join(", ", arguments)}) is ambiguous, please provide more arguments. Possible fields {ListFields(identifier)}");
                }
                return probableFields.First();
            }
            throw new EntityGraphQLCompilerException($"Field {identifier}({string.Join(", ", arguments)}) not found");
        }

        private string ListFields(string identifier)
        {
            var fields = _fieldsByName[identifier].Select(f => f.Name + "(" + string.Join(", ", f.Arguments.Values) + ")");
            return string.Join(", ", fields);
        }

        public IEnumerable<Field> GetFields()
        {
            return _fieldsByKey.Values;
        }
        /// <summary>
        /// Checks if type has a field with the given name and the given arguments
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public bool HasField(string identifier, params string[] arguments)
        {
            var key = Field.MakeFieldKey(identifier, arguments);
            return _fieldsByKey.ContainsKey(key);
        }

        public void RemoveField(string name, params string[] arguments)
        {
            var key = Field.MakeFieldKey(name, arguments);
            if (_fieldsByKey.ContainsKey(key))
            {
                var oldField = _fieldsByKey[key];
                _fieldsByKey.Remove(key);
                if (_fieldsByName.ContainsKey(name))
                {
                    _fieldsByName[name].Remove(oldField);
                }
            }
        }
        public void RemoveField(Expression<Func<TBaseType, object>> fieldSelection)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            RemoveField(exp.Member.Name);
        }
    }
}