using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Authorization;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Describes an entity field. It's expression based on the base type (your data model) and it's mapped return type
    /// </summary>
    public class Field : IField
    {
        private readonly Dictionary<string, ArgType> allArguments = new Dictionary<string, ArgType>();

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

        public object ArgumentTypesObject { get; private set; }
        public IDictionary<string, ArgType> Arguments { get { return allArguments; } }

        public IEnumerable<string> RequiredArgumentNames
        {
            get
            {
                if (ArgumentTypesObject == null)
                    return new List<string>();

                var required = ArgumentTypesObject.GetType().GetTypeInfo().GetFields().Where(f => f.FieldType.IsConstructedGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(RequiredField<>)).Select(f => f.Name);
                var requiredProps = ArgumentTypesObject.GetType().GetTypeInfo().GetProperties().Where(f => f.PropertyType.IsConstructedGenericType && f.PropertyType.GetGenericTypeDefinition() == typeof(RequiredField<>)).Select(f => f.Name);
                return required.Concat(requiredProps).ToList();
            }
        }

        public GqlTypeInfo ReturnType { get; }

        internal Field(string name, LambdaExpression resolve, string description, GqlTypeInfo returnType, RequiredClaims authorizeClaims)
        {
            Name = name;
            Description = description;
            AuthorizeClaims = authorizeClaims;
            ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType), "retypeType can not be null");

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

        public Field(ISchemaProvider schema, string name, LambdaExpression resolve, string description, object argTypes, GqlTypeInfo returnType, RequiredClaims claims) : this(name, resolve, description, returnType, claims)
        {
            ArgumentTypesObject = argTypes;
            allArguments = argTypes.GetType().GetProperties().ToDictionary(p => p.Name, p => ArgType.FromProperty(schema, p));
            argTypes.GetType().GetFields().ToDictionary(p => p.Name, p => ArgType.FromField(schema, p)).ToList().ForEach(kvp => allArguments.Add(kvp.Key, kvp.Value));
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
    }
}