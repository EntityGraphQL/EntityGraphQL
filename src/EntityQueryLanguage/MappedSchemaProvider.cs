using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using EntityQueryLanguage.Schema;
using EntityQueryLanguage.Util;

namespace EntityQueryLanguage
{
    /// Builder interface to build a schema definition. The built schema definition maps an external view of your data model to you internal model.
    /// This allows your internal model to change over time while not break your external API. You can create new versions when needed.
    public class MappedSchemaProvider<TContextType> : ISchemaProvider
    {
        protected readonly EqlType<TContextType> _queryContext;
        protected Dictionary<string, IEqlType> _types = new Dictionary<string, IEqlType>(StringComparer.OrdinalIgnoreCase);

        public MappedSchemaProvider()
        {
            _queryContext = new EqlType<TContextType>(typeof(TContextType).Name, "Query schema");
        }

        /// Add a new Object type into the schema with TBaseType as it's context
        public EqlType<TBaseType> AddType<TBaseType>(string name, string description)
        {
			return AddType<TBaseType>(name, description, null);
        }

        public EqlType<TBaseType> AddType<TBaseType>(string name, string description, Expression<Func<TBaseType, bool>> filter)
        {
			var tt = new EqlType<TBaseType>(name, description, filter);
            _types.Add(name, tt);
			return tt;
        }

        public EqlType<TBaseType> AddType<TBaseType>(string description, Expression<Func<TBaseType, bool>> filter = null)
        {
            var name = typeof(TBaseType).Name;
            return AddType(name, description, filter);
        }

        /// Add a "field" to the root of the object graph. This is where you define top level objects/names that they can query
        public void AddField(Expression<Func<TContextType, object>> selection, string description, string returnSchemaType = null)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(selection);
            AddField(exp.Member.Name, selection, description, returnSchemaType);
        }
        public void AddField(Field field)
        {
            _queryContext.AddField(field);
        }

        public void AddField(string name, Expression<Func<TContextType, object>> selection, string description, string returnSchemaType = null)
        {
            _queryContext.AddField(name, selection, description, returnSchemaType);
        }

        /// Get registered type by TType name
        public EqlType<TType> Type<TType>()
        {
            return (EqlType<TType>)_types[typeof(TType).Name];
        }

        // ISchemaProvider interface
        public Type ContextType { get { return _queryContext.ContextType; } }
        public bool TypeHasField(string typeName, string identifier)
        {
            if (_queryContext.ContextType.Name.ToLower() == typeName.ToLower())
                return _queryContext.HasField(identifier);

            return (_types.ContainsKey(typeName) && _types[typeName].HasField(identifier))
                 || (typeName == _queryContext.ContextType.Name && _queryContext.HasField(identifier));
        }
		public bool TypeHasField(Type type, string identifier)
        {
            return TypeHasField(type.Name, identifier);
        }
        public string GetActualFieldName(string typeName, string identifier)
        {
            if (_types.ContainsKey(typeName) && _types[typeName].HasField(identifier))
                return _types[typeName].GetField(identifier).Name;
            if (typeName == _queryContext.ContextType.Name && _queryContext.HasField(identifier))
                return _queryContext.GetField(identifier).Name;
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