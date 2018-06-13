using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityQueryLanguage.Extensions;
using EntityQueryLanguage.Schema;
using EntityQueryLanguage.Util;

namespace EntityQueryLanguage.Schema
{
    /// <summary>
    /// Builder interface to build a schema definition. The built schema definition maps an external view of your data model to you internal model.
    /// This allows your internal model to change over time while not break your external API. You can create new versions when needed.
    /// </summary>
    /// <typeparam name="TContextType"></typeparam>
    public class MappedSchemaProvider<TContextType> : ISchemaProvider
    {
        protected Dictionary<string, ISchemaType> _types = new Dictionary<string, ISchemaType>(StringComparer.OrdinalIgnoreCase);
        private readonly string _queryContextName;

        public MappedSchemaProvider()
        {
            var queryContext = new SchemaType<TContextType>(typeof(TContextType).Name, "Query schema");
            _queryContextName = queryContext.Name;
            _types.Add(queryContext.Name, queryContext);
        }

        /// <summary>
        /// Add a new type into the schema with TBaseType as it's context
        /// </summary>
        /// <param name="name">Name of the type</param>
        /// <param name="description">description of the type</param>
        /// <typeparam name="TBaseType"></typeparam>
        /// <returns></returns>
        public SchemaType<TBaseType> AddType<TBaseType>(string name, string description)
        {
			return AddType<TBaseType>(name, description, null);
        }

        /// <summary>
        /// Add a new type into the schema with an optional filter applied to the result
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="filter"></param>
        /// <typeparam name="TBaseType"></typeparam>
        /// <returns></returns>
        public SchemaType<TBaseType> AddType<TBaseType>(string name, string description, Expression<Func<TBaseType, bool>> filter)
        {
			var tt = new SchemaType<TBaseType>(name, description, filter);
            _types.Add(name, tt);
			return tt;
        }

        /// <summary>
        /// Adds a new type into the schema. The name defaults to the TBaseType nmae
        /// </summary>
        /// <param name="description"></param>
        /// <param name="filter"></param>
        /// <typeparam name="TBaseType"></typeparam>
        /// <returns></returns>
        public SchemaType<TBaseType> AddType<TBaseType>(string description, Expression<Func<TBaseType, bool>> filter = null)
        {
            var name = typeof(TBaseType).Name;
            return AddType(name, description, filter);
        }

        /// <summary>
        /// Add a field to the root type. This is where you define top level objects/names that you can query.
        /// The name defaults to the MemberExpression from selection
        /// </summary>
        /// <param name="selection"></param>
        /// <param name="description"></param>
        /// <param name="returnSchemaType"></param>
        public void AddField(Expression<Func<TContextType, object>> selection, string description, string returnSchemaType = null)
        {
            var exp = ExpressionUtil.CheckAndGetMemberExpression(selection);
            AddField(exp.Member.Name, selection, description, returnSchemaType);
        }

        /// <summary>
        /// Add a field to the root type. This is where you define top level objects/names that you can query.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selection"></param>
        /// <param name="description"></param>
        /// <param name="returnSchemaType"></param>
        public void AddField(string name, Expression<Func<TContextType, object>> selection, string description, string returnSchemaType = null)
        {
            Type<TContextType>().AddField(name, selection, description, returnSchemaType);
        }

        /// <summary>
        /// Add a field with arguments.
        /// {
        ///     field(arg: val) {}
        /// }
        /// </summary>
        /// <param name="name">Field name</param>
        /// <param name="argTypes">Anonymous object defines the names and types of each argument</param>
        /// <param name="selectionExpression">The expression that selects the data from TContextType using the arguments</param>
        /// <param name="returnSchemaType">The schema type to return, it defines the fields available on the return object. If null, defaults to TReturn type mapped in the schema.</param>
        /// <typeparam name="TParams">Type describing the arguments</typeparam>
        /// <typeparam name="TReturn">The return entity type that is mapped to a type in the schema</typeparam>
        /// <returns></returns>
        public void AddField<TParams, TReturn>(string name, TParams argTypes, Expression<Func<TContextType, TParams, TReturn>> selectionExpression, string description, string returnSchemaType = null)
        {
            Type<TContextType>().AddField(name, argTypes, selectionExpression, description, returnSchemaType);
        }

        public void AddField(Field field)
        {
            _types[_queryContextName].AddField(field);
        }

        /// <summary>
        /// Get registered type by TType name
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <returns></returns>
        public SchemaType<TType> Type<TType>()
        {
            return (SchemaType<TType>)_types[typeof(TType).Name];
        }

        // ISchemaProvider interface
        public Type ContextType { get { return _types[_queryContextName].ContextType; } }
        public bool TypeHasField(string typeName, string identifier)
        {
            if (_queryContextName.ToLower() == typeName.ToLower())
                return _types[_queryContextName].HasField(identifier);

            return (_types.ContainsKey(typeName) && _types[typeName].HasField(identifier))
                 || (typeName == _queryContextName && _types[_queryContextName].HasField(identifier));
        }
		public bool TypeHasField(Type type, string identifier)
        {
            return TypeHasField(type.Name, identifier);
        }
        public string GetActualFieldName(string typeName, string identifier)
        {
            if (_types.ContainsKey(typeName) && _types[typeName].HasField(identifier))
                return _types[typeName].GetField(identifier).Name;
            if (typeName == _queryContextName && _types[_queryContextName].HasField(identifier))
                return _types[_queryContextName].GetField(identifier).Name;
            throw new EqlCompilerException($"Field {identifier} not found on any type");
        }

        public ExpressionResult GetExpressionForField(Expression context, string typeName, string fieldName, Dictionary<string, ExpressionResult> args)
        {
            if (!_types.ContainsKey(typeName))
                throw new EntityQuerySchemaError($"{typeName} not found in schema.");
            var field = _types[typeName].GetField(fieldName);
            var result = new ExpressionResult(field.Resolve ?? Expression.Property(context, fieldName));

            if (args != null)
            {
                // get the values for the argument object constructor
                var vals = new List<object>();
                foreach (var argField in field.ArgumentTypes.GetType().GetProperties())
                {
                    string argName = argField.Name.ToLower();
                    // check we have required arguments
                    if (argField.PropertyType.GetGenericTypeDefinition() == typeof(RequiredField<>))
                    {
                        if (!args.ContainsKey(argName))
                        {
                            throw new EqlCompilerException($"Missing required argument '{argName}' for field '{field.Name}'");
                        }
                        var item = Expression.Lambda(args[argName]).Compile().DynamicInvoke();
                        // explicitly cast the value to the RequiredField<> type
                        var typedVal = Activator.CreateInstance(argField.PropertyType, item);
                        vals.Add(typedVal);
                    }
                    else if (args.ContainsKey(argName))
                    {
                        vals.Add(Expression.Lambda(args[argName]).Compile().DynamicInvoke());
                    }
                    else
                    {
                        // set the default value
                        vals.Add(argField.GetValue(field.ArgumentTypes));
                    }
                }

                // create a copy of the anonymous object. It will have the default values set
                // there is only 1 constructor for the anonymous type that takes all the property values
                var con = field.ArgumentTypes.GetType().GetConstructors().First();
                var parameters = con.Invoke(vals.ToArray());
                // tell them this expression has another parameter
                var argParam = Expression.Parameter(field.ArgumentTypes.GetType());
                result.AddParameter(argParam, parameters);
            }

            // the expressions we collect have a different starting parameter. We need to change that
            var paramExp = field.FieldParam;
            result.Expression = new ParameterReplacer().Replace(result.Expression, paramExp, context);

            return result;
        }
        public string GetSchemaTypeNameForRealType(Type type)
        {
            if (type == _types[_queryContextName].ContextType)
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