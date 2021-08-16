using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Describes an entity field. It's expression based on the base type (your data model) and it's mapped return type
    /// </summary>
    public class Field : IField
    {
        private readonly Dictionary<string, ArgType> allArguments = new Dictionary<string, ArgType>();
        private readonly ISchemaProvider schema;

        public string Name { get; internal set; }
        public ParameterExpression FieldParam { get; private set; }

        public List<IFieldExtension> Extensions { get; private set; }
        public RequiredClaims AuthorizeClaims { get; private set; }

        public Expression Resolve { get; private set; }
        /// <summary>
        /// Services required to be injected for this fields selection
        /// </summary>
        /// <value></value>
        public IEnumerable<Type> Services { get; private set; }
        public string Description { get; private set; }

        public IDictionary<string, ArgType> Arguments { get { return allArguments; } }
        public Type ArgumentsType { get; private set; }

        public IEnumerable<string> RequiredArgumentNames
        {
            get
            {
                if (allArguments == null)
                    return new List<string>();

                var required = allArguments.Where(f => f.Value.Type.TypeDotnet.IsConstructedGenericType && f.Value.Type.TypeDotnet.GetGenericTypeDefinition() == typeof(RequiredField<>)).Select(f => f.Key);
                return required.ToList();
            }
        }

        public GqlTypeInfo ReturnType { get; private set; }

        internal Field(ISchemaProvider schema, string name, LambdaExpression resolve, string description, GqlTypeInfo returnType, RequiredClaims authorizeClaims)
        {
            this.schema = schema;
            Name = name;
            Description = description;
            AuthorizeClaims = authorizeClaims;
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

                if (resolve.Body.NodeType == ExpressionType.MemberAccess)
                {
                    ReturnType.TypeNotNullable = GraphQLNotNullAttribute.IsMemberMarkedNotNull(((MemberExpression)resolve.Body).Member) || ReturnType.TypeNotNullable;
                    ReturnType.ElementTypeNullable = GraphQLElementTypeNullable.IsMemberElementMarkedNullable(((MemberExpression)resolve.Body).Member) || ReturnType.ElementTypeNullable;
                }
            }
        }

        public void AddArguments(object args)
        {
            var newArgs = ExpressionUtil.ObjectToDictionaryArgs(schema, args);
            ArgumentsType = ExpressionUtil.MergeTypes(ArgumentsType, args.GetType());
            newArgs.ToList().ForEach(k => allArguments.Add(k.Key, k.Value));
        }

        public void UpdateReturnType(GqlTypeInfo gqlTypeInfo)
        {
            ReturnType = gqlTypeInfo;
        }

        public void UpdateExpression(Expression expression)
        {
            Resolve = expression;
        }


        public Field(ISchemaProvider schema, string name, LambdaExpression resolve, string description, object argTypes, GqlTypeInfo returnType, RequiredClaims claims)
            : this(schema, name, resolve, description, returnType, claims)
        {
            allArguments = ExpressionUtil.ObjectToDictionaryArgs(schema, argTypes);
            ArgumentsType = argTypes.GetType();
        }

        public bool HasArgumentByName(string argName)
        {
            return allArguments.ContainsKey(argName);
        }

        public ArgType GetArgumentType(string argName)
        {
            return allArguments[argName];
        }

        /// <summary>
        /// To access this field all claims listed here are required
        /// </summary>
        /// <param name="claims"></param>
        /// <returns></returns>
        public Field RequiresAllClaims(params string[] claims)
        {
            if (AuthorizeClaims == null)
                AuthorizeClaims = new RequiredClaims();
            AuthorizeClaims.RequiresAllClaims(claims);
            return this;
        }

        /// <summary>
        /// To access this field any claims listed is required
        /// </summary>
        /// <param name="claims"></param>
        /// <returns></returns>
        public Field RequiresAnyClaim(params string[] claims)
        {
            if (AuthorizeClaims == null)
                AuthorizeClaims = new RequiredClaims();
            AuthorizeClaims.RequiresAnyClaim(claims);
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

        public ExpressionResult GetExpression(Expression context, Dictionary<string, ExpressionResult> args)
        {
            var result = new ExpressionResult(Resolve, Services);

            var parameterReplacer = new ParameterReplacer();
            PrepareExpressionResult(args, this, result, parameterReplacer, context);
            // the expressions we collect have a different starting parameter. We need to change that
            result.Expression = parameterReplacer.Replace(result.Expression, FieldParam, context);
            return result;
        }


        private void PrepareExpressionResult(Dictionary<string, ExpressionResult> args, Field field, ExpressionResult result, ParameterReplacer parameterReplacer, Expression context)
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
                            genericProp.SetValue(entityQuery, ((ExpressionResult)val).Expression);
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
                        item.Key.SetValue(argumentValues, Convert.ChangeType(item.Value, item.Key.FieldType));
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
                var argParam = Expression.Parameter(field.ArgumentsType, $"arg_{field.ArgumentsType.Name}");
                result.Expression = parameterReplacer.ReplaceByType(result.Expression, field.ArgumentsType, argParam);
                result.AddConstantParameter(argParam, argumentValues);

                if (Extensions.Count > 0)
                {
                    foreach (var m in Extensions)
                    {
                        m.GetExpression(this, result, argParam, argumentValues, context, parameterReplacer);
                    }
                }
            }
            else
            {
                if (Extensions.Count > 0)
                {
                    foreach (var m in Extensions)
                    {
                        m.GetExpression(this, (ExpressionResult)Resolve, null, null, context, parameterReplacer);
                    }
                }
            }
        }


        private static object BuildArgumentFromMember(Dictionary<string, ExpressionResult> args, Field field, string memberName, Type memberType, object defaultValue)
        {
            string argName = memberName;
            // check we have required arguments
            if (memberType.GetGenericArguments().Any() && memberType.GetGenericTypeDefinition() == typeof(RequiredField<>))
            {
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