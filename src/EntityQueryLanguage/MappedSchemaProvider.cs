using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EntityQueryLanguage {
	/// Builder interface to build a schema definition. The built schema definition maps an external view of your data model to you internal model.
	/// This allows your internal model to change over time while not break your external API. You can create new versions when needed.
	public class MappedSchemaProvider : ISchemaProvider {
		private BaseEqlType _queryContext;
		private Dictionary<string, BaseEqlType> _types = new Dictionary<string, BaseEqlType>(StringComparer.OrdinalIgnoreCase); 
		
		/// Define a type in the schema. Fields are taken from the type T
		public void Type<TBaseType>(string name, string description) {
			_types.Add(name, new EqlType<TBaseType>(name, description));
		}
		/// Define a type in the schema. Fields are defined
		public void Type<TBaseType>(string name, string description, object fields) {
			_types.Add(name, new EqlType<TBaseType>(name, description, BuildFields(fields)));
		}
		/// Define the base query fields
		public void Query<TContextType>(object fields) {
			_queryContext = new EqlType<TContextType>(typeof(TContextType).Name, "Query context", BuildFields(fields));
		}
		public Field Field<TContext, TFieldType>(Expression<Func<TContext, TFieldType>> resolve, string description) {
			return new Field(resolve, description, null);
		}
		public Field Field<TContext, TFieldType>(Expression<Func<TContext, TFieldType>> resolve, string description, string type) {
			return new Field(resolve, description, type);
		}
		
		// ISchemaProvider interface
		public Type ContextType { get { return _queryContext.ContextType; } }
		public bool TypeHasField(string typeName, string identifier) {
	     	return (_types.ContainsKey(typeName) && _types[typeName].HasField(identifier))
		  		|| (typeName == _queryContext.ContextType.Name && _queryContext.HasField(identifier));
	    }
		public string GetActualFieldName(string typeName, string identifier) {
	     	if (_types.ContainsKey(typeName) && _types[typeName].HasField(identifier))
				return _types[typeName].GetField(identifier).Name;
			if (typeName == _queryContext.ContextType.Name && _queryContext.HasField(identifier))
				return _queryContext.GetField(identifier).Name;
			throw new EqlCompilerException($"Field {identifier} not found on any type");
	    }
		public Expression GetExpressionForField(Expression context, string typeName, string field) {
			// the expressions we collect have a different starting parameter. We need to change that
			Expression result;
			ParameterExpression paramExp;
			if (typeName == _queryContext.ContextType.Name) {
				result = _queryContext.GetField(field).Resolve;
				paramExp = _queryContext.GetField(field).FieldParam;
			}
			else {
				result = _types[typeName].GetField(field).Resolve;
				paramExp = _types[typeName].GetField(field).FieldParam;
			}
			
			result = new ParameterReplacer().Replace(result, paramExp, context);

			return result;
	    }
    	public string GetSchemaTypeNameForRealType(Type type) {
			if (type == _queryContext.ContextType)
				return type.Name;

			foreach (var eType in _types.Values) {
				if (eType.ContextType == type)
					return eType.Name;
			}
	    	throw new EqlCompilerException($"No mapped entity found for type '{type}'");
	    }

		private List<Field> BuildFields(object fieldsObj) {
			var fieldList = new List<Field>();
			foreach (var prop in fieldsObj.GetType().GetProperties()) {
				var field = prop.GetValue(fieldsObj) as Field;
				field.Name = prop.Name;
				fieldList.Add(field);
			}
			return fieldList;
		}
	}
	
	/// Describes an entity field. It's expression based on the base type (yoru data model) and it's mapped return type
	public class Field {
        public string Name { get; set; }
		public ParameterExpression FieldParam { get; private set; }
		internal Field(LambdaExpression resolve, string description, string type) {
			Resolve = resolve.Body;
			Type = type;
			Description = description;
			FieldParam = resolve.Parameters.First();
		}
		public Expression Resolve { get; private set; }
		public string Type { get; private set; }
		public string Description { get; private set; }
	}
	
	public abstract class BaseEqlType {
		public abstract Type ContextType { get; protected set; }
		public abstract string Name { get; protected set; }

        internal abstract Field GetField(string identifier);

        internal abstract bool HasField(string identifier);
    }
	public class EqlType<TBaseContext> : BaseEqlType {
		public override Type ContextType { get; protected set; }
        public override string Name { get; protected set; }
        private string _description;
		private Dictionary<string, Field> _fields = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
		
		public EqlType() {
			ContextType = typeof(TBaseContext);
			BuildFieldsFromBase<TBaseContext>();
		}

        public EqlType(string name, string description)
        {
			ContextType = typeof(TBaseContext);
            Name = name;
            _description = description;
			BuildFieldsFromBase<TBaseContext>();
        }
		public EqlType(string name, string description, IEnumerable<Field> fields)
        {
			ContextType = typeof(TBaseContext);
            Name = name;
            _description = description;
			foreach (var f in fields) {
				_fields.Add(f.Name, f);			
			}
        }
		private void BuildFieldsFromBase<TContext>() {
			foreach (var f in ContextType.GetProperties()) {
				if (!_fields.ContainsKey(f.Name)) {
					var parameter = Expression.Parameter(ContextType);
					_fields.Add(f.Name, new Field(Expression.Lambda(Expression.Property(parameter, f.Name), parameter), string.Empty, f.PropertyType.ToString()) { Name = f.Name });
				}
			}
		}
		internal override Field GetField(string identifier) {
			return _fields[identifier];
		}
		internal override bool HasField(string identifier) {
			return _fields.ContainsKey(identifier);
		}
	}
	
	
	/// As people build schema fields they are against a different parameter, this visitor lets us change it to the one used in compiling the EQL
	internal class ParameterReplacer : ExpressionVisitor {
		private Expression _newParam;
		private ParameterExpression _toReplace;
		internal Expression Replace(Expression node, ParameterExpression toReplace, Expression newParam) {
			_newParam = newParam;
			_toReplace = toReplace;
			return Visit(node);
		}
		
		protected override Expression VisitParameter(ParameterExpression node) {
			if (_toReplace == node)
				return _newParam;
			return node;
		}
	}
}