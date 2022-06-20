using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
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
        public override GraphQLQueryFieldType FieldType { get; } = GraphQLQueryFieldType.Query;

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

        public override (Expression? expression, object? argumentValues) GetExpression(Expression fieldExpression, Expression? fieldContext, IGraphQLNode? parentNode, ParameterExpression? schemaContext, Dictionary<string, object> args, ParameterExpression? docParam, object? docVariables, IEnumerable<GraphQLDirective> directives, bool contextChanged, ParameterReplacer replacer)
        {
            Expression? expression = fieldExpression;
            foreach (var directive in directives)
            {
                expression = directive.Process(Schema, fieldExpression, args, docParam, docVariables);
            }
            if (expression == null)
                return (null, null);
            var result = expression;
            // don't store parameterReplacer as a class field as GetExpression is caleld in compiling - i.e. across threads
            (result, var argumentValues) = PrepareFieldExpression(args, this, result, replacer, expression, parentNode, docParam, docVariables, contextChanged);
            if (result == null)
                return (null, null);
            // the expressions we collect have a different starting parameter. We need to change that
            if (FieldParam != null && !contextChanged)
            {
                if (fieldContext != null)
                    result = replacer.Replace(result, FieldParam, fieldContext);
                else if (parentNode?.NextFieldContext != null)
                    result = replacer.Replace(result, FieldParam, parentNode.NextFieldContext);
            }
            // need to make sure the schema context param is correct
            if (schemaContext != null && !contextChanged)
                result = replacer.ReplaceByType(result, schemaContext.Type, schemaContext);

            return (result, argumentValues);
        }

        private (Expression? fieldExpression, object? arguments) PrepareFieldExpression(Dictionary<string, object> args, Field field, Expression? fieldExpression, ParameterReplacer parameterReplacer, Expression context, IGraphQLNode? parentNode, ParameterExpression? docParam, object? docVariables, bool servicesPass)
        {
            object? argumentValues = null;
            Expression? result = fieldExpression;
            if (field.ArgumentsType != null && FieldParam != null)
            {
                argumentValues = ArgumentUtil.BuildArgumentsObject(field.Schema, field.Name, field, args, field.Arguments.Values, field.ArgumentsType, docParam, docVariables);
            }
            if (Extensions.Count > 0)
            {
                foreach (var m in Extensions)
                {
                    if (result != null)
                        result = m.GetExpression(this, result, ArgumentParam, argumentValues, context, parentNode, servicesPass, parameterReplacer);
                }
            }

            if (argumentValidators.Count > 0)
            {
                var invokeContext = new ArgumentValidatorContext(field, argumentValues);
                foreach (var m in argumentValidators)
                {
                    m(invokeContext);
                    argumentValues = invokeContext.Arguments;
                }
                if (invokeContext.Errors.Any())
                    throw new EntityGraphQLValidationException(invokeContext.Errors);
            }
            return (result, argumentValues);
        }

        protected void SetUpField(LambdaExpression fieldExpression)
        {
            ProcessResolveExpression(fieldExpression);
            // Because we use the return type as object to make the compile time interface nicer we need to get the real return type
            var returnType = fieldExpression.Body.Type;
            if (fieldExpression.Body.NodeType == ExpressionType.Convert)
                returnType = ((UnaryExpression)fieldExpression.Body).Operand.Type;

            if (fieldExpression.Body.NodeType == ExpressionType.Call)
                returnType = ((MethodCallExpression)fieldExpression.Body).Type;

            if (typeof(Task).IsAssignableFrom(returnType))
                throw new EntityGraphQLCompilerException($"Field {Name} is returning a Task please resolve your async method with .GetAwaiter().GetResult()");

            ReturnType = SchemaBuilder.MakeGraphQlType(Schema, returnType, null);
        }
    }
}