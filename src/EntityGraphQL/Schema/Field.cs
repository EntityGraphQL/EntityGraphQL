using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Describes an entity field. It's expression based on the base type (your data model) and it's mapped return type
    /// </summary>
    public class Field : IMethodType
    {
        private readonly Dictionary<string, ArgType> allArguments = new Dictionary<string, ArgType>();
        private string returnTypeSingle;

        public string Name { get; internal set; }
        public ParameterExpression FieldParam { get; private set; }
        public bool ReturnTypeNotNullable { get; set; }
        public bool ReturnElementTypeNullable { get; set; }

        public RequiredClaims AuthorizeClaims { get; private set; }

        public Expression Resolve { get; private set; }
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

        public Type ReturnTypeClr { get; private set; }

        internal Field(string name, LambdaExpression resolve, string description, string returnSchemaType, Type returnClrType, RequiredClaims authorizeClaims)
        {
            Name = name;
            Description = description;
            returnTypeSingle = returnSchemaType;
            ReturnTypeClr = returnClrType;
            AuthorizeClaims = authorizeClaims;

            if (resolve != null)
            {
                Resolve = resolve.Body;
                FieldParam = resolve.Parameters.First();
                ReturnTypeClr = Resolve.Type;

                if (resolve.Body.NodeType == ExpressionType.MemberAccess)
                {
                    ReturnTypeNotNullable = GraphQLNotNullAttribute.IsMemberMarkedNotNull(((MemberExpression)resolve.Body).Member);
                    ReturnElementTypeNullable = GraphQLElementTypeNullable.IsMemberElementMarkedNullable(((MemberExpression)resolve.Body).Member);
                }
            }
        }

        public Field(string name, LambdaExpression resolve, string description, string returnSchemaType, object argTypes, RequiredClaims claims) : this(name, resolve, description, returnSchemaType, null, claims)
        {
            ArgumentTypesObject = argTypes;
            allArguments = argTypes.GetType().GetProperties().ToDictionary(p => p.Name, p => new ArgType
            {
                Type = p.PropertyType,
                TypeNotNullable = GraphQLNotNullAttribute.IsMemberMarkedNotNull(p),
            });
            argTypes.GetType().GetFields().ToDictionary(p => p.Name, p => new ArgType
            {
                Type = p.FieldType,
                TypeNotNullable = GraphQLNotNullAttribute.IsMemberMarkedNotNull(p),
            }).ToList().ForEach(kvp => allArguments.Add(kvp.Key, kvp.Value));
        }

        public string GetReturnType(ISchemaProvider schema)
        {
            if (!string.IsNullOrEmpty(returnTypeSingle))
                return returnTypeSingle;
            return schema.GetSchemaTypeNameForClrType(ReturnTypeClr.GetNonNullableOrEnumerableType());
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