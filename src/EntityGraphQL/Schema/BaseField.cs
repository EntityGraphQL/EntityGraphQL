using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Schema
{
    public abstract class BaseField : IField
    {
        public abstract FieldType FieldType { get; }
        public ParameterExpression? FieldParam { get; set; }

        public string Description { get; protected set; }
        public IDictionary<string, ArgType> Arguments { get; set; } = new Dictionary<string, ArgType>();
        public ParameterExpression? ArgumentParam { get; set; }
        public string Name { get; internal set; }
        public GqlTypeInfo ReturnType { get; protected set; }
        public List<IFieldExtension> Extensions { get; set; }
        public RequiredAuthorization? RequiredAuthorization { get; protected set; }
        public bool IsDeprecated { get; set; }
        public string? DeprecationReason { get; set; }

        /// <summary>
        /// If true the arguments on the field are used internally for processing (usually in extensions that change the 
        /// shape of the schema and need arguments from the original field)
        /// Arguments will not be in introspection
        /// </summary>
        public bool ArgumentsAreInternal { get; internal set; }

        /// <summary>
        /// Services required to be injected for this fields selection
        /// </summary>
        /// <value></value>
        public IEnumerable<Type> Services { get; set; } = new List<Type>();

        public Expression? Resolve { get; protected set; }

        public ISchemaProvider Schema { get; set; }
        public Type? ArgumentsType { get; set; }

        protected BaseField(ISchemaProvider schema, string name, string? description, GqlTypeInfo returnType)
        {
            this.Schema = schema;
            Description = description ?? string.Empty;
            Name = name;
            ReturnType = returnType;
            Extensions = new List<IFieldExtension>();
        }

        protected static object BuildArgumentsObject(Dictionary<string, object> args, IField field, ParameterExpression? docParam, object? docVariables)
        {
            // get the values for the argument anonymous type object constructor
            var propVals = new Dictionary<PropertyInfo, object?>();
            var fieldVals = new Dictionary<FieldInfo, object?>();
            // if they used AddField("field", new { id = Required<int>() }) the compiler makes properties and a constructor with the values passed in
            foreach (var argField in field.Arguments.Values)
            {
                object? val;
                try
                {
                    if (args.ContainsKey(argField.Name) && args[argField.Name] is Expression expression)
                    {
                        // this value comes from the variables from the query document
                        if (docVariables != null)
                            val = Expression.Lambda((Expression)args[argField.Name], docParam).Compile().DynamicInvoke(new[] { docVariables });
                        else
                            val = args[argField.Name];
                        propVals.Add((PropertyInfo)argField.MemberInfo!, ExpressionUtil.ChangeType(val, ((PropertyInfo)argField.MemberInfo!).PropertyType, field.Schema));
                    }
                    else
                    {
                        val = BuildArgumentFromMember(args, field, argField.Name, argField.RawType, argField.DefaultValue);
                        // if this was a EntityQueryType we actually get a Func from BuildArgumentFromMember but the anonymous type requires EntityQueryType<>. We marry them here, this allows users to EntityQueryType<> as a Func in LINQ methods while not having it defined until runtime
                        if (argField.Type.TypeDotnet.IsConstructedGenericType && argField.Type.TypeDotnet.GetGenericTypeDefinition() == typeof(EntityQueryType<>))
                        {
                            if (argField.MemberInfo is PropertyInfo info)
                                propVals.Add((PropertyInfo)argField.MemberInfo, ExpressionUtil.ChangeType(val, ((PropertyInfo)argField.MemberInfo).PropertyType, field.Schema));
                            else
                                fieldVals.Add((FieldInfo)argField.MemberInfo!, ExpressionUtil.ChangeType(val, ((FieldInfo)argField.MemberInfo!).FieldType, field.Schema));
                        }
                        else
                        {
                            // this could be int to RequiredField<int>
                            if (val != null && val.GetType() != argField.RawType)
                                val = ExpressionUtil.ChangeType(val, argField.RawType, field.Schema);
                            if (argField.MemberInfo is PropertyInfo info)
                                propVals.Add(info, val);
                            else
                                fieldVals.Add((FieldInfo)argField.MemberInfo!, val);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new EntityGraphQLCompilerException($"Variable or value used for argument '{argField.Name}' does not match argument type '{argField.Type}'", ex);
                }
            }
            // create a copy of the anonymous object. It will have the default values set
            // there is only 1 constructor for the anonymous type that takes all the property values
            var con = field.ArgumentsType!.GetConstructor(propVals.Keys.Select(v => v.PropertyType).ToArray());
            object argumentValues;
            if (con != null)
            {
                argumentValues = con.Invoke(propVals.Values.ToArray());
                foreach (var item in fieldVals)
                {
                    item.Key.SetValue(argumentValues, item.Value);
                }
            }
            else
            {
                // expect an empty constructor
                con = field.ArgumentsType.GetConstructor(new Type[0]);
                argumentValues = con.Invoke(new object[0]);
                foreach (var item in fieldVals)
                {
                    item.Key.SetValue(argumentValues, item.Value);
                }
                foreach (var item in propVals)
                {
                    item.Key.SetValue(argumentValues, item.Value);
                }
            }

            return argumentValues;
        }

        private static object? BuildArgumentFromMember(Dictionary<string, object>? args, IField field, string memberName, Type memberType, object? defaultValue)
        {
            string argName = memberName;
            // check we have required arguments
            if (memberType.GetGenericArguments().Any() && memberType.GetGenericTypeDefinition() == typeof(RequiredField<>))
            {
                // shouldn't get here as QueryWalkerHelper.CheckRequiredArguments is called in the compiler
                // but just incase
                if (args == null || !args.ContainsKey(argName))
                {
                    throw new EntityGraphQLCompilerException($"Field '{field.Name}' missing required argument '{argName}'");
                }
                var item = args[argName];
                var constructor = memberType.GetConstructor(new[] { item.GetType() });
                if (constructor == null)
                {
                    // we might need to change the type
                    foreach (var c in memberType.GetConstructors())
                    {
                        var parameters = c.GetParameters();
                        if (parameters.Count() == 1)
                        {
                            item = ExpressionUtil.ChangeType(item, parameters[0].ParameterType, field.Schema);
                            constructor = memberType.GetConstructor(new[] { item?.GetType() });
                            break;
                        }
                    }
                }

                if (constructor == null)
                {
                    throw new EntityGraphQLCompilerException($"Could not find a constructor for type {memberType.Name} that takes value '{item}'");
                }

                var typedVal = constructor.Invoke(new[] { item });
                return typedVal;
            }
            else if (defaultValue != null && defaultValue.GetType().IsConstructedGenericType && defaultValue.GetType().GetGenericTypeDefinition() == typeof(EntityQueryType<>))
            {
                return args != null && args.ContainsKey(argName) ? args[argName] : null;
            }
            else if (args != null && args.ContainsKey(argName))
            {
                return args[argName];
            }
            else
            {
                // set the default value
                return defaultValue;
            }
        }

        /// <summary>
        /// Add a field extension to this field
        /// </summary>
        /// <param name="extension"></param>
        public void AddExtension(IFieldExtension extension)
        {
            Extensions.Add(extension);
            extension.Configure(Schema, this);
        }

        public ArgType GetArgumentType(string argName)
        {
            return Arguments[argName];
        }

        public abstract ExpressionResult GetExpression(Expression fieldExpression, Expression? fieldContext, IGraphQLNode? parentNode, ParameterExpression? schemaContext, Dictionary<string, object> args, ParameterExpression? docParam, object? docVariables, bool contextChanged);
        public bool HasArgumentByName(string argName)
        {
            return Arguments.ContainsKey(argName);
        }

        /// <summary>
        /// Update the expression used to resolve this fields value
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public IField UpdateExpression(Expression expression)
        {
            Resolve = expression;
            return this;
        }

        /// <summary>
        /// Adds a argument object to the field. The fields on the object will be added as arguments.
        /// Any exisiting arguments with the same name will be overwritten.
        /// </summary>
        /// <param name="args"></param>
        public void AddArguments(object args)
        {
            // get new argument values
            var newArgs = ExpressionUtil.ObjectToDictionaryArgs(Schema, args, Schema.SchemaFieldNamer);
            // build new argument Type
            var newArgType = ExpressionUtil.MergeTypes(ArgumentsType, args.GetType());
            // Update the values - we don't read new values from this as the type has now lost any default values etc but we have them in allArguments
            newArgs.ToList().ForEach(k => Arguments.Add(k.Key, k.Value));
            // now we need to update the MemberInfo
            foreach (var item in Arguments)
            {
                item.Value.MemberInfo = (MemberInfo)newArgType.GetProperty(item.Value.DotnetName) ??
                    newArgType.GetField(item.Value.DotnetName);
            }
            var parameterReplacer = new ParameterReplacer();

            var argParam = Expression.Parameter(newArgType, $"arg_{newArgType.Name}");
            if (ArgumentParam != null && Resolve != null)
                Resolve = parameterReplacer.Replace(Resolve, ArgumentParam, argParam);

            ArgumentParam = argParam;
            ArgumentsType = newArgType;
        }
        public IField Returns(GqlTypeInfo gqlTypeInfo)
        {
            ReturnType = gqlTypeInfo;
            return this;
        }
    }
}