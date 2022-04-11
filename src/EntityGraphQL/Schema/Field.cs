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
    /// <summary>
    /// Describes an entity field. It's expression based on the base type (your data model) and it's mapped return type
    /// </summary>
    public class Field : IField
    {
        public IDictionary<string, ArgType> Arguments { get; set; } = new Dictionary<string, ArgType>();

        public ParameterExpression ArgumentParam { get; set; }
        private readonly ISchemaProvider schema;
        private readonly Func<string, string> fieldNamer;

        public string Name { get; internal set; }
        public ParameterExpression FieldParam { get; set; }
        public List<IFieldExtension> Extensions { get; set; }

        public RequiredAuthorization RequiredAuthorization { get; private set; }

        public bool IsDeprecated { get; set; }
        public string DeprecationReason { get; set; }

        public Expression Resolve { get; private set; }
        /// <summary>
        /// Services required to be injected for this fields selection
        /// </summary>
        /// <value></value>
        public IEnumerable<Type> Services { get; set; }
        public string Description { get; private set; }

        public Type ArgumentsType { get; set; }

        public IEnumerable<string> RequiredArgumentNames
        {
            get
            {
                if (Arguments == null)
                    return new List<string>();

                var required = Arguments.Where(f => f.Value.Type.TypeDotnet.IsConstructedGenericType && f.Value.Type.TypeDotnet.GetGenericTypeDefinition() == typeof(RequiredField<>)).Select(f => f.Key);
                return required.ToList();
            }
        }

        public GqlTypeInfo ReturnType { get; private set; }
        /// <summary>
        /// If true the arguments on the field are used internally for processing (usually in extensions that change the 
        /// shape of the schema and need arguments from the original field)
        /// Arguments will not be in introspection
        /// </summary>
        public bool ArgumentsAreInternal { get; internal set; }

        internal Field(ISchemaProvider schema, string name, LambdaExpression resolve, string description, GqlTypeInfo returnType, RequiredAuthorization requiredAuth, Func<string, string> fieldNamer)
        {
            this.schema = schema;
            Name = name;
            Description = description;
            RequiredAuthorization = requiredAuth;
            this.fieldNamer = fieldNamer;
            ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType), "retypeType can not be null");
            Extensions = new List<IFieldExtension>();

            if (resolve != null)
            {
                if (resolve.Body.NodeType == ExpressionType.Call && ((MethodCallExpression)resolve.Body).Method.DeclaringType == typeof(ArgumentHelper) && ((MethodCallExpression)resolve.Body).Method.Name == "WithService")
                {
                    // they are wanting services injected
                    var call = (MethodCallExpression)resolve.Body;
                    var lambdaExpression = (LambdaExpression)((UnaryExpression)call.Arguments.First()).Operand;
                    Resolve = lambdaExpression.Body;
                    Services = lambdaExpression.Parameters.Select(p => p.Type).ToList();
                }
                else
                {
                    Resolve = resolve.Body;
                }
                FieldParam = resolve.Parameters.First();
                ArgumentParam = resolve.Parameters.Count == 1 ? null : resolve.Parameters.ElementAt(1);

                if (resolve.Body.NodeType == ExpressionType.MemberAccess)
                {
                    var memberExp = (MemberExpression)resolve.Body;
                    ReturnType.TypeNotNullable = GraphQLNotNullAttribute.IsMemberMarkedNotNull(memberExp.Member) || ReturnType.TypeNotNullable;
                    ReturnType.ElementTypeNullable = GraphQLElementTypeNullable.IsMemberElementMarkedNullable(memberExp.Member) || ReturnType.ElementTypeNullable;

                    var obsoleteAttribute = memberExp.Member.GetCustomAttribute<ObsoleteAttribute>();
                    if (obsoleteAttribute != null)
                    {
                        IsDeprecated = true;
                        DeprecationReason = obsoleteAttribute.Message;
                    }
                }
            }
        }

        public void Deprecate(string reason)
        {
            IsDeprecated = true;
            DeprecationReason = reason;
        }

        public void AddArguments(object args)
        {
            // get new argument values
            var newArgs = ExpressionUtil.ObjectToDictionaryArgs(schema, args, fieldNamer);
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
            Resolve = parameterReplacer.Replace(Resolve, ArgumentParam, argParam);
            ArgumentParam = argParam;
            ArgumentsType = newArgType;
        }

        public void UpdateReturnType(GqlTypeInfo gqlTypeInfo)
        {
            ReturnType = gqlTypeInfo;
        }

        public IField UpdateExpression(Expression expression)
        {
            Resolve = expression;
            return this;
        }


        public Field(ISchemaProvider schema, string name, LambdaExpression resolve, string description, object argTypes, GqlTypeInfo returnType, RequiredAuthorization claims, Func<string, string> fieldNamer)
            : this(schema, name, resolve, description, returnType, claims, fieldNamer)
        {
            Arguments = ExpressionUtil.ObjectToDictionaryArgs(schema, argTypes, fieldNamer);
            ArgumentsType = argTypes.GetType();
        }

        public bool HasArgumentByName(string argName)
        {
            return Arguments.ContainsKey(argName);
        }

        public ArgType GetArgumentType(string argName)
        {
            return Arguments[argName];
        }

        /// <summary>
        /// To access this field all claims listed here are required
        /// </summary>
        /// <param name="claims"></param>
        [Obsolete("Use RequiresAllRoles")]
        public Field RequiresAllClaims(params string[] claims)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAllRoles(claims);
            return this;
        }

        /// <summary>
        /// To access this field any claims listed is required
        /// </summary>
        /// <param name="claims"></param>
        [Obsolete("Use RequiresAnyRole")]
        public Field RequiresAnyClaim(params string[] claims)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAnyRole(claims);
            return this;
        }

        /// <summary>
        /// To access this field all roles listed here are required
        /// </summary>
        /// <param name="roles"></param>
        public Field RequiresAllRoles(params string[] roles)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAllRoles(roles);
            return this;
        }

        /// <summary>
        /// To access this field any role listed is required
        /// </summary>
        /// <param name="roles"></param>
        public Field RequiresAnyRole(params string[] roles)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAllRoles(roles);
            return this;
        }

        /// <summary>
        /// To access this field all policies listed here are required
        /// </summary>
        /// <param name="policies"></param>
        public Field RequiresAllPolicies(params string[] policies)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAllPolicies(policies);
            return this;
        }

        /// <summary>
        /// To access this field any policy listed is required
        /// </summary>
        /// <param name="policies"></param>
        public Field RequiresAnyPolicy(params string[] policies)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAnyPolicy(policies);
            return this;
        }

        /// <summary>
        /// Clears any authorization requirements for this field
        /// </summary>
        /// <returns></returns>
        public Field ClearAuthorization()
        {
            RequiredAuthorization = null;
            return this;
        }

        /// <summary>
        /// Define if the return type of this field is nullable or not.
        /// </summary>
        /// <param name="nullable"></param>
        /// <returns></returns>
        public Field IsNullable(bool nullable)
        {
            ReturnType.TypeNotNullable = !nullable;

            return this;
        }

        public void AddExtension(IFieldExtension extension)
        {
            Extensions.Add(extension);
            extension.Configure(schema, this);
        }

        public ExpressionResult GetExpression(Expression fieldExpression, Expression fieldContext, IGraphQLNode parentNode, ParameterExpression schemaContext, Dictionary<string, Expression> args, bool contextChanged)
        {
            var result = new ExpressionResult(fieldExpression, Services);
            // don't store parameterReplacer as a class field as GetExpression is caleld in compiling - i.e. across threads
            var parameterReplacer = new ParameterReplacer();
            PrepareExpressionResult(args, this, result, parameterReplacer, fieldExpression, parentNode, contextChanged);
            // the expressions we collect have a different starting parameter. We need to change that
            if (FieldParam != null && !contextChanged)
            {
                result.Expression = parameterReplacer.Replace(result.Expression, FieldParam, fieldContext ?? parentNode.NextFieldContext);
            }
            // need to make sure the schema context param is correct
            if (schemaContext != null && !contextChanged)
                result.Expression = parameterReplacer.ReplaceByType(result.Expression, schemaContext.Type, schemaContext);

            return result;
        }


        private void PrepareExpressionResult(Dictionary<string, Expression> args, Field field, ExpressionResult result, ParameterReplacer parameterReplacer, Expression context, IGraphQLNode parentNode, bool servicesPass)
        {
            if (field.ArgumentsType != null)
            {
                // get the values for the argument anonymous type object constructor
                var propVals = new Dictionary<PropertyInfo, object>();
                var fieldVals = new Dictionary<FieldInfo, object>();
                // if they used AddField("field", new { id = Required<int>() }) the compiler makes properties and a constructor with the values passed in
                foreach (var argField in field.Arguments.Values)
                {
                    var val = BuildArgumentFromMember(args, field, argField.Name, argField.RawType, argField.DefaultValue);
                    // if this was a EntityQueryType we actually get a Func from BuildArgumentFromMember but the anonymous type requires EntityQueryType<>. We marry them here, this allows users to EntityQueryType<> as a Func in LINQ methods while not having it defined until runtime
                    if (argField.Type.TypeDotnet.IsConstructedGenericType && argField.Type.TypeDotnet.GetGenericTypeDefinition() == typeof(EntityQueryType<>))
                    {
                        // make sure we create a new instance and not update the schema
                        var entityQuery = Activator.CreateInstance(argField.Type.TypeDotnet);

                        // set Query
                        var hasValue = val != null;
                        if (hasValue)
                        {
                            var genericProp = entityQuery.GetType().GetProperty("Query");
                            genericProp.SetValue(entityQuery, val);
                        }

                        if (argField.MemberInfo is PropertyInfo info)
                            propVals.Add(info, entityQuery);
                        else
                            fieldVals.Add((FieldInfo)argField.MemberInfo, entityQuery);
                    }
                    else
                    {
                        // this could be int to RequiredField<int>
                        if (val != null && val.GetType() != argField.RawType)
                            val = ExpressionUtil.ChangeType(val, argField.RawType);
                        if (argField.MemberInfo is PropertyInfo info)
                            propVals.Add(info, val);
                        else
                            fieldVals.Add((FieldInfo)argField.MemberInfo, val);
                    }
                }
                // create a copy of the anonymous object. It will have the default values set
                // there is only 1 constructor for the anonymous type that takes all the property values
                var con = field.ArgumentsType.GetConstructor(propVals.Keys.Select(v => v.PropertyType).ToArray());
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
                // tell them this expression has another parameter
                result.AddConstantParameter(ArgumentParam, argumentValues);
                if (Extensions.Count > 0)
                {
                    foreach (var m in Extensions)
                    {
                        result.Expression = m.GetExpression(this, result.Expression, ArgumentParam, argumentValues, context, parentNode, servicesPass, parameterReplacer);
                    }
                }
            }
            else
            {
                if (Extensions.Count > 0)
                {
                    foreach (var m in Extensions)
                    {
                        result.Expression = m.GetExpression(this, result.Expression, ArgumentParam, null, context, parentNode, servicesPass, parameterReplacer);
                    }
                }
            }
        }


        private static object BuildArgumentFromMember(Dictionary<string, Expression> args, Field field, string memberName, Type memberType, object defaultValue)
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
                var item = Expression.Lambda(args[argName]).Compile().DynamicInvoke();
                var constructor = memberType.GetConstructor(new[] { item.GetType() });
                if (constructor == null)
                {
                    // we might need to change the type
                    foreach (var c in memberType.GetConstructors())
                    {
                        var parameters = c.GetParameters();
                        if (parameters.Count() == 1)
                        {
                            item = ExpressionUtil.ChangeType(item, parameters[0].ParameterType);
                            constructor = memberType.GetConstructor(new[] { item.GetType() });
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
                return Expression.Lambda(args[argName]).Compile().DynamicInvoke();
            }
            else
            {
                // set the default value
                return defaultValue;
            }
        }
    }
}