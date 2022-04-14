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
    public class Field : BaseField, IField
    {
        public override FieldType FieldType { get; } = FieldType.Query;

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

        internal Field(ISchemaProvider schema, string name, LambdaExpression? resolve, string? description, GqlTypeInfo returnType, RequiredAuthorization? requiredAuth)
        : base(schema, name, description, returnType)
        {
            RequiredAuthorization = requiredAuth;
            Extensions = new List<IFieldExtension>();

            if (resolve != null)
            {
                ProcessResolveExpression(resolve);
            }
        }

        protected void ProcessResolveExpression(LambdaExpression resolve)
        {
            ResolveExpression = resolve.Body;
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

        public Field(ISchemaProvider schema, string name, LambdaExpression? resolve, string? description, object? argTypes, GqlTypeInfo returnType, RequiredAuthorization? claims)
            : this(schema, name, resolve, description, returnType, claims)
        {
            if (argTypes != null)
            {
                Arguments = ExpressionUtil.ObjectToDictionaryArgs(schema, argTypes, schema.SchemaFieldNamer);
                ArgumentsType = argTypes.GetType();
            }
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
        /// Marks this field as deprecated
        /// </summary>
        /// <param name="reason"></param>
        public void Deprecate(string reason)
        {
            IsDeprecated = true;
            DeprecationReason = reason;
        }

        /// <summary>
        /// Defines if the return type of this field is nullable or not.
        /// </summary>
        /// <param name="nullable"></param>
        /// <returns></returns>
        public Field IsNullable(bool nullable)
        {
            ReturnType.TypeNotNullable = !nullable;

            return this;
        }

        /// <summary>
        /// Update the return type information for this field
        /// </summary>
        /// <param name="gqlTypeInfo"></param>
        public new Field Returns(GqlTypeInfo gqlTypeInfo)
        {
            return (Field)base.Returns(gqlTypeInfo);
        }

        /// <summary>
        /// Update the return Type of this field
        /// </summary>
        /// <param name="schemaTypeName"></param>
        /// <returns></returns>
        public Field Returns(string schemaTypeName)
        {
            Returns(new GqlTypeInfo(() => Schema.Type(schemaTypeName), Schema.Type(schemaTypeName).TypeDotnet));
            return this;
        }

        public override ExpressionResult? GetExpression(Expression fieldExpression, Expression? fieldContext, IGraphQLNode? parentNode, ParameterExpression? schemaContext, Dictionary<string, object> args, ParameterExpression? docParam, object? docVariables, IEnumerable<GraphQLDirective> directives, bool contextChanged)
        {
            Expression? expression = fieldExpression;
            foreach (var directive in directives)
            {
                expression = directive.Process(Schema, fieldExpression, args, docParam, docVariables);
            }
            if (expression == null)
                return null;
            var result = new ExpressionResult(expression, Services);
            // don't store parameterReplacer as a class field as GetExpression is caleld in compiling - i.e. across threads
            var parameterReplacer = new ParameterReplacer();
            PrepareExpressionResult(args, this, result, parameterReplacer, expression, parentNode, docParam, docVariables, contextChanged);
            // the expressions we collect have a different starting parameter. We need to change that
            if (FieldParam != null && !contextChanged)
            {
                if (fieldContext != null)
                    result.Expression = parameterReplacer.Replace(result.Expression, FieldParam, fieldContext);
                else if (parentNode?.NextFieldContext != null)
                    result.Expression = parameterReplacer.Replace(result.Expression, FieldParam, parentNode.NextFieldContext);
            }
            // need to make sure the schema context param is correct
            if (schemaContext != null && !contextChanged)
                result.Expression = parameterReplacer.ReplaceByType(result.Expression, schemaContext.Type, schemaContext);

            return result;
        }

        private void PrepareExpressionResult(Dictionary<string, object> args, Field field, ExpressionResult result, ParameterReplacer parameterReplacer, Expression context, IGraphQLNode? parentNode, ParameterExpression? docParam, object? docVariables, bool servicesPass)
        {
            if (field.ArgumentsType != null && args != null && FieldParam != null)
            {
                object argumentValues = ArgumentUtil.BuildArgumentsObject(field.Schema, field.Name, args, field.Arguments.Values, field.ArgumentsType, docParam, docVariables);
                if (ArgumentParam != null)
                {
                    // tell them this expression has another parameter
                    result.AddConstantParameter(ArgumentParam, argumentValues);
                }
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
    }
}