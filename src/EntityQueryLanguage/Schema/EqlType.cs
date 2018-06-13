using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using EntityQueryLanguage.Util;

namespace EntityQueryLanguage.Schema
{
    public interface IEqlType
    {
        Type ContextType { get; }
        string Name { get; }

        Field GetField(string identifier);
        bool HasField(string identifier);
        void AddFields(List<Field> fields);
        void AddField(Field field);
    }
    public class EqlType<TBaseType> : IEqlType
    {
        public Type ContextType { get; protected set; }
        public string Name { get; protected set; }
        private string _description;
        private Dictionary<string, Field> _fields = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
        private readonly Expression<Func<TBaseType, bool>> _filter;

        public EqlType()
        {
            ContextType = typeof(TBaseType);
        }

        public EqlType(string name, string description, Expression<Func<TBaseType, bool>> filter = null) : this()
        {
            Name = name;
            _description = description;
            _filter = filter;
        }

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
            _fields.Add(field.Name, field);
        }
        public void AddField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> fieldSelection, string description, string returnSchemaType = null)
        {
            var field = new Field(name, fieldSelection, description, returnSchemaType);
            _fields.Add(field.Name, field);
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
            _fields.Add(field.Name, field);
        }

        private void BuildFieldsFromBase(Type contextType)
        {
            foreach (var f in ContextType.GetProperties())
            {
                if (!_fields.ContainsKey(f.Name))
                {
                    var parameter = Expression.Parameter(ContextType);
                    _fields.Add(f.Name, new Field(f.Name, Expression.Lambda(Expression.Property(parameter, f.Name), parameter), string.Empty, string.Empty));
                }
            }
            foreach (var f in ContextType.GetFields())
            {
                if (!_fields.ContainsKey(f.Name))
                {
                    var parameter = Expression.Parameter(ContextType);
                    _fields.Add(f.Name, new Field(f.Name, Expression.Lambda(Expression.Field(parameter, f.Name), parameter), string.Empty, string.Empty));
                }
            }
        }

        public Field GetField(string identifier)
        {
            return _fields[identifier];
        }
        public bool HasField(string identifier)
        {
            return _fields.ContainsKey(identifier);
        }

        public void RemoveField(string name)
        {
            if (_fields.ContainsKey(name))
                _fields.Remove(name);
        }
        public void RemoveField(Expression<Func<TBaseType, object>> fieldSelection)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(fieldSelection);
            RemoveField(exp.Member.Name);
        }
    }
}