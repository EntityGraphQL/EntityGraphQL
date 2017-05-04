using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace EntityQueryLanguage.Schema
{
    public class EqlType
    {
        public Type ContextType { get; protected set; }
        public string Name { get; protected set; }
        private string _description;
        private Dictionary<string, Field> _fields = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);

        public EqlType(Type contextType)
        {
            ContextType = contextType;
            BuildFieldsFromBase(contextType);
        }

        public EqlType(Type contextType, string name, string description) : this(contextType)
        {
            Name = name;
            _description = description;
            BuildFieldsFromBase(contextType);
        }
        public EqlType(Type contextType, string name, string description, IEnumerable<Field> fields)
        {
            ContextType = contextType;
            Name = name;
            _description = description;
            foreach (var f in fields)
            {
                _fields.Add(f.Name, f);
            }
        }

		internal void AddField(Field field)
		{
			_fields.Add(field.Name, field);
		}

        private void BuildFieldsFromBase(Type contextType)
        {
            foreach (var f in ContextType.GetProperties())
            {
                if (!_fields.ContainsKey(f.Name))
                {
                    var parameter = Expression.Parameter(ContextType);
                    _fields.Add(f.Name, new Field(f.Name, Expression.Lambda(Expression.Property(parameter, f.Name), parameter), string.Empty));
                }
            }
            foreach (var f in ContextType.GetFields())
            {
                if (!_fields.ContainsKey(f.Name))
                {
                    var parameter = Expression.Parameter(ContextType);
                    _fields.Add(f.Name, new Field(f.Name, Expression.Lambda(Expression.Field(parameter, f.Name), parameter), string.Empty));
                }
            }
        }
        internal Field GetField(string identifier)
        {
            return _fields[identifier];
        }
        internal bool HasField(string identifier)
        {
            return _fields.ContainsKey(identifier);
        }

        internal void RemoveField(string name)
        {
            if (_fields.ContainsKey(name))
                _fields.Remove(name);
        }
    }
}