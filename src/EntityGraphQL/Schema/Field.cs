using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
        private readonly List<IFieldExtension> extensions;
        private readonly ISchemaProvider schema;

        public string Name { get; internal set; }
        public ParameterExpression FieldParam { get; private set; }

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
            extensions = new List<IFieldExtension>();

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
            extensions.Add(extension);
            extension.Configure(schema, this);
        }

        public ExpressionResult GetExpression()
        {
            if (extensions.Count > 0)
            {
                foreach (var m in extensions)
                {
                    m.Invoke(this);
                }
            }
            var res = new ExpressionResult(Resolve, Services);
            return res;
        }
    }
}