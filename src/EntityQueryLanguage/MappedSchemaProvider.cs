using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EntityQueryLanguage
{
    /// Builder interface to build a schema definition. The built schema definition maps an external view of your data model to you internal model.
    /// This allows your internal model to change over time while not break your external API. You can create new versions when needed.
    public class MappedSchemaProvider : ISchemaProvider
    {
        protected EqlType _queryContext;
        protected Dictionary<string, EqlType> _types = new Dictionary<string, EqlType>(StringComparer.OrdinalIgnoreCase);

        /// Get the EqlType for the given TBaseType. Allowing you to extend it etc.
        public EqlType Type<TBaseType>()
        {
            return Type(typeof(TBaseType).Name);
        }
        public EqlType Type(string typeName)
        {
            return _types[typeName];
        }

        /// Define a type in the schema. Fields are taken from the type T
        public EqlType Type<TBaseType>(string name, string description)
        {
			var tt = new EqlType(typeof(TBaseType), name, description);
            _types.Add(name, tt);
			return tt;
        }
        public EqlType Type(Type contextType, string name, string description)
        {
			var tt = new EqlType(contextType, name, description);
            _types.Add(name, tt);
			return tt;
        }
        /// Define a type in the schema. Fields are defined
        public EqlType Type<TBaseType>(string name, string description, object fields)
        {
			var tt = new EqlType(typeof(TBaseType), name, description, BuildFields(fields));
            _types.Add(name, tt);
			return tt;
        }

        /// Define the base root of the schema
        public void BuildSchema<TContextType>(object fields)
        {
            if (fields is List<Field>)
                _queryContext = new EqlType(typeof(TContextType), typeof(TContextType).Name, "Query context", (List<Field>)fields);
            else
                _queryContext = new EqlType(typeof(TContextType), typeof(TContextType).Name, "Query context", BuildFields(fields));
            _types.Add(_queryContext.Name, _queryContext);
        }

        public void BuildSchema(Type contextType, List<Field> fields)
        {
            _queryContext = new EqlType(contextType, contextType.Name, "Query context", fields);
            _types.Add(_queryContext.Name, _queryContext);
        }

        public Field Field<TContext, TFieldType>(Expression<Func<TContext, TFieldType>> resolve, string description)
        {
            return new Field(resolve, description);
        }
        public Field Field<TContext, TFieldType>(Expression<Func<TContext, TFieldType>> resolve, string description, string type)
        {
            return new Field(resolve, description);
        }

        // ISchemaProvider interface
        public Type ContextType { get { return _queryContext.ContextType; } }
        public bool TypeHasField(string typeName, string identifier)
        {
            return (_types.ContainsKey(typeName) && _types[typeName].HasField(identifier))
                 || (typeName == _queryContext.ContextType.Name && _queryContext.HasField(identifier));
        }
		public bool TypeHasField(Type type, string identifier)
        {
			var typeName = type.Name.ToLower();
            return TypeHasField(typeName, identifier);
        }
        public string GetActualFieldName(string typeName, string identifier)
        {
            if (_types.ContainsKey(typeName) && _types[typeName].HasField(identifier))
                return _types[typeName].GetField(identifier).Name;
            if (typeName == _queryContext.ContextType.Name && _queryContext.HasField(identifier))
                return _queryContext.GetField(identifier).Name;
            throw new EqlCompilerException($"Field {identifier} not found on any type");
        }
        public Field GetField(string typeName, string identifier)
        {
            if (_types.ContainsKey(typeName) && _types[typeName].HasField(identifier))
                return _types[typeName].GetField(identifier);
            if (typeName == _queryContext.ContextType.Name && _queryContext.HasField(identifier))
                return _queryContext.GetField(identifier);
            throw new EqlCompilerException($"Field {identifier} not found on any type");
        }

        public Expression GetExpressionForField(Expression context, string typeName, string field)
        {
            // the expressions we collect have a different starting parameter. We need to change that
            Expression result;
            ParameterExpression paramExp;
            if (typeName == _queryContext.ContextType.Name)
            {
                result = _queryContext.GetField(field).Resolve;
                paramExp = _queryContext.GetField(field).FieldParam;
            }
            else
            {
                if (!_types.ContainsKey(typeName))
                    throw new EntityQuerySchemaError($"{typeName} not found in schema.");
                result = _types[typeName].GetField(field).Resolve ?? Expression.Property(context, field);
                paramExp = _types[typeName].GetField(field).FieldParam;
            }

            result = new ParameterReplacer().Replace(result, paramExp, context);

            return result;
        }
        public string GetSchemaTypeNameForRealType(Type type)
        {
            if (type == _queryContext.ContextType)
                return type.Name;

            foreach (var eType in _types.Values)
            {
                if (eType.ContextType == type)
                    return eType.Name;
            }
            throw new EqlCompilerException($"No mapped entity found for type '{type}'");
        }

        private List<Field> BuildFields(object fieldsObj)
        {
            var fieldList = new List<Field>();
            foreach (var prop in fieldsObj.GetType().GetProperties())
            {
                var field = prop.GetValue(fieldsObj) as Field;
                field.Name = prop.Name;
                fieldList.Add(field);
            }
            return fieldList;
        }

        public bool HasType(string typeName)
        {
            return _types.ContainsKey(typeName);
        }
    }

    /// Describes an entity field. It's expression based on the base type (your data model) and it's mapped return type
    public class Field
    {
        public string Name { get; set; }
        public ParameterExpression FieldParam { get; private set; }
        internal Field(LambdaExpression resolve, string description)
        {
            Resolve = resolve.Body;
            Description = description;
            FieldParam = resolve.Parameters.First();
        }
        public Expression Resolve { get; private set; }
        public string Description { get; private set; }
    }

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
                    _fields.Add(f.Name, new Field(Expression.Lambda(Expression.Property(parameter, f.Name), parameter), string.Empty) { Name = f.Name });
                }
            }
            foreach (var f in ContextType.GetFields())
            {
                if (!_fields.ContainsKey(f.Name))
                {
                    var parameter = Expression.Parameter(ContextType);
                    _fields.Add(f.Name, new Field(Expression.Lambda(Expression.Field(parameter, f.Name), parameter), string.Empty) { Name = f.Name });
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
    }


    /// As people build schema fields they are against a different parameter, this visitor lets us change it to the one used in compiling the EQL
    internal class ParameterReplacer : ExpressionVisitor
    {
        private Expression _newParam;
        private ParameterExpression _toReplace;
        internal Expression Replace(Expression node, ParameterExpression toReplace, Expression newParam)
        {
            _newParam = newParam;
            _toReplace = toReplace;
            return Visit(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (_toReplace == node)
                return _newParam;
            return node;
        }
    }
}