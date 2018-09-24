using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityQueryLanguage.Compiler;
using EntityQueryLanguage.Extensions;
using EntityQueryLanguage.Schema;

namespace EntityQueryLanguage.Schema
{
    /// <summary>
    /// Builder interface to build a schema definition. The built schema definition maps an external view of your data model to you internal model.
    /// This allows your internal model to change over time while not break your external API. You can create new versions when needed.
    /// </summary>
    /// <typeparam name="TContextType">Base object graph. Ex. DbContext</typeparam>
    public class MappedSchemaProvider<TContextType> : ISchemaProvider
    {
        protected Dictionary<string, ISchemaType> _types = new Dictionary<string, ISchemaType>(StringComparer.OrdinalIgnoreCase);
        protected Dictionary<string, ISchemaType> _mutations = new Dictionary<string, ISchemaType>(StringComparer.OrdinalIgnoreCase);
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

        public void AddMutationFrom<TType>(TType mutationClassInstance)
        {
            foreach (var method in mutationClassInstance.GetType().GetMethods())
            {
                var attribute = method.GetCustomAttribute(typeof(GraphQLMutationAttribute));
                if (attribute != null)
                {
                    _mutations[method.Name] = new MutationType(_types[GetSchemaTypeNameForRealType(method.ReturnType)], mutationClassInstance, method);
                }
            }
        }

        public bool HasMutation(string method)
        {
            return _mutations.ContainsKey(method);
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
        public ISchemaType Type(string typeName)
        {
            return _types[typeName];
        }
        // ISchemaProvider interface
        public Type ContextType { get { return _types[_queryContextName].ContextType; } }
        public bool TypeHasField(string typeName, string identifier)
        {
            return _types.ContainsKey(typeName) && _types[typeName].HasField(identifier);
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

        public IMethodType GetMethodType(Expression context, string fieldName)
        {
            if (_mutations.ContainsKey(fieldName))
            {
                var mutation = _mutations[fieldName];
                return (IMethodType)mutation;
            }
            if (_types.ContainsKey(context.Type.Name))
            {
                var field = _types[context.Type.Name].GetField(fieldName);
                return field;
            }
            throw new EqlCompilerException($"No field or mutation '{fieldName}' found in schema.");
        }

        public ExpressionResult GetExpressionForField(Expression context, string typeName, string fieldName, Dictionary<string, ExpressionResult> args)
        {
            if (!_types.ContainsKey(typeName))
                throw new EntityQuerySchemaError($"{typeName} not found in schema.");
            var field = _types[typeName].GetField(fieldName);
            var result = new ExpressionResult(field.Resolve ?? Expression.Property(context, fieldName));

            if (field.ArgumentTypes != null)
            {
                var argType = field.ArgumentTypes.GetType();
                // get the values for the argument anonymous type object constructor
                var propVals = new Dictionary<PropertyInfo, object>();
                var fieldVals = new Dictionary<FieldInfo, object>();
                // if they used AddField("field", new { id = Required<int>() }) the compiler makes properties and a constructor with the
                foreach (var argField in argType.GetProperties())
                {
                    var val = BuildArgumentFromMember(args, field, argField.Name, argField.PropertyType, argField.GetValue(field.ArgumentTypes));
                    propVals.Add(argField, val);
                }
                // The auto argument is built at runtime from LinqRuntimeTypeBuilder which just makes public fields
                // they could also use a custom class
                foreach (var argField in argType.GetFields())
                {
                    var val = BuildArgumentFromMember(args, field, argField.Name, argField.FieldType, argField.GetValue(field.ArgumentTypes));
                    fieldVals.Add(argField, val);
                }

                // create a copy of the anonymous object. It will have the default values set
                // there is only 1 constructor for the anonymous type that takes all the property values
                var con = argType.GetConstructor(propVals.Values.Select(v => v.GetType()).ToArray());
                object parameters;
                if (con != null)
                {
                    parameters = con.Invoke(propVals.Values.ToArray());
                    foreach (var item in fieldVals)
                    {
                        item.Key.SetValue(parameters, item.Value);
                    }
                }
                else
                {
                    // expect an empty constructor
                    con = argType.GetConstructor(new Type[0]);
                    parameters = con.Invoke(new object[0]);
                    foreach (var item in fieldVals)
                    {
                        item.Key.SetValue(parameters, item.Value);
                    }
                    foreach (var item in propVals)
                    {
                        item.Key.SetValue(parameters, item.Value);
                    }
                }
                // tell them this expression has another parameter
                var argParam = Expression.Parameter(argType);
                result.Expression = new ParameterReplacer().ReplaceByType(result.Expression, argType, argParam);
                result.AddConstantParameter(argParam, parameters);
            }

            // the expressions we collect have a different starting parameter. We need to change that
            var paramExp = field.FieldParam;
            result.Expression = new ParameterReplacer().Replace(result.Expression, paramExp, context);

            return result;
        }

        private static object BuildArgumentFromMember(Dictionary<string, ExpressionResult> args, Field field, string memberName, Type memberType, object defaultValue)
        {
            string argName = memberName.ToLower();
            // check we have required arguments
            if (memberType.GetGenericArguments().Any() && memberType.GetGenericTypeDefinition() == typeof(RequiredField<>))
            {
                if (args == null || !args.ContainsKey(argName))
                {
                    throw new EqlCompilerException($"Missing required argument '{argName}' for field '{field.Name}'");
                }
                var item = Expression.Lambda(args[argName]).Compile().DynamicInvoke();
                // explicitly cast the value to the RequiredField<> type
                var typedVal = Activator.CreateInstance(memberType, item);
                return typedVal;
            }
            else if (args != null && args.ContainsKey(argName))
            {
                return Expression.Lambda(args[argName]).Compile().DynamicInvoke();
            }
            else
            {
                // set the default value
                return defaultValue;
            }
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
}