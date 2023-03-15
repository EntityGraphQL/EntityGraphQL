using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Describes an entity field. It's expression based on the base type (your data model) and it's mapped return type
    /// </summary>
    public class Field : BaseField
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

        protected void ProcessResolveExpression(LambdaExpression resolve, bool withServices, bool hasArguments)
        {
            ResolveExpression = resolve.Body;
            FieldParam = resolve.Parameters.First();
            if (hasArguments)
                ArgumentParam = resolve.Parameters.ElementAt(1);

            if (resolve.Body.NodeType == ExpressionType.MemberAccess)
            {
                var memberExp = (MemberExpression)resolve.Body;
                ReturnType.TypeNotNullable = GraphQLNotNullAttribute.IsMemberMarkedNotNull(memberExp.Member) || ReturnType.TypeNotNullable;
                ReturnType.ElementTypeNullable = GraphQLElementTypeNullableAttribute.IsMemberElementMarkedNullable(memberExp.Member) || ReturnType.ElementTypeNullable;
            }

            if (withServices)
            {
                var extractor = new ExpressionExtractor();
                ExtractedFieldsFromServices = extractor.Extract(ResolveExpression, FieldParam, false)?.Select(i => new GraphQLExtractedField(Schema, i.Key, i.Value, FieldParam)).ToList();
            }
        }

        public Field(ISchemaProvider schema, ISchemaType fromType, string name, LambdaExpression? resolve, string? description, object? argTypes, GqlTypeInfo returnType, RequiredAuthorization? requiredAuth)
        : base(schema, fromType, name, description, returnType)
        {
            RequiredAuthorization = requiredAuth;
            Extensions = new List<IFieldExtension>();

            if (resolve != null)
            {
                ProcessResolveExpression(resolve, false, argTypes != null);
            }
            if (argTypes != null)
            {
                Arguments = ExpressionUtil.ObjectToDictionaryArgs(schema, argTypes);
                ArgumentsType = argTypes.GetType();
            }
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

        public override (Expression? expression, ParameterExpression? argumentParam) GetExpression(Expression fieldExpression, Expression? fieldContext, IGraphQLNode? parentNode, ParameterExpression? schemaContext, CompileContext? compileContext, IReadOnlyDictionary<string, object> args, ParameterExpression? docParam, object? docVariables, IEnumerable<GraphQLDirective> directives, bool contextChanged, ParameterReplacer replacer)
        {
            Expression? expression = fieldExpression;
            foreach (var directive in directives)
            {
                expression = directive.Process(Schema, fieldExpression, args, docParam, docVariables);
            }
            if (expression == null)
                return (null, null);

            // don't store parameterReplacer as a class field as GetExpression is caleld in compiling - i.e. across threads
            (var result, var argumentParam) = PrepareFieldExpression(args, this, expression!, replacer, expression, parentNode, docParam, docVariables, contextChanged, compileContext);
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

            return (result, argumentParam);
        }

        private (Expression? fieldExpression, ParameterExpression? argumentParam) PrepareFieldExpression(IReadOnlyDictionary<string, object> args, Field field, Expression fieldExpression, ParameterReplacer replacer, Expression context, IGraphQLNode? parentNode, ParameterExpression? docParam, object? docVariables, bool servicesPass, CompileContext? compileContext)
        {
            object? argumentValues = null;
            Expression? result = fieldExpression;
            var validationErrors = new List<string>();
            var newArgParam = ArgumentParam;
            // check if we are taking args from elsewhere (extensions do this)
            if (UseArgumentsFromField != null && compileContext != null)
            {
                newArgParam = compileContext.GetConstantParameterForField(UseArgumentsFromField);
                if (newArgParam == null)
                    throw new EntityGraphQLCompilerException($"Could not find arguments for field {UseArgumentsFromField.Name} in compile context.");
                argumentValues = compileContext.ConstantParameters[newArgParam];
            }
            else
            {
                if (field.ArgumentsType != null && FieldParam != null)
                {
                    argumentValues = ArgumentUtil.BuildArgumentsObject(field.Schema, field.Name, field, args, field.Arguments.Values, field.ArgumentsType, docParam, docVariables, validationErrors);
                }
                // we need to make a copy of the argument parameter as if they select the same field multiple times
                // i.e. with different alias & arguments we need to have different ParameterExpression instances
                if (ArgumentParam != null)
                {
                    newArgParam = Expression.Parameter(ArgumentParam.Type, $"{ArgumentParam.Name}_exec");
                }
                if (argumentValues != null && compileContext != null)
                    compileContext.AddConstant(this, newArgParam!, argumentValues);
            }

            if (Extensions.Count > 0)
            {
                foreach (var m in Extensions)
                {
                    if (result != null)
                        result = m.GetExpression(this, result, newArgParam, argumentValues, context, parentNode, servicesPass, replacer);
                }
            }

            // replace the arg param after extensions (don't rely on extensions to do this)
            if (ArgumentParam != null)
            {
                result = replacer.Replace(result, ArgumentParam, newArgParam!);
            }

            if (ArgumentValidators.Count > 0)
            {
                var invokeContext = new ArgumentValidatorContext(field, argumentValues);
                foreach (var m in ArgumentValidators)
                {
                    m(invokeContext);
                    argumentValues = invokeContext.Arguments;
                }

                validationErrors.AddRange(invokeContext.Errors);
            }

            if (validationErrors.Count > 0)
            {
                throw new EntityGraphQLValidationException(validationErrors);
            }

            return (result, newArgParam);
        }

        protected void SetUpField(LambdaExpression fieldExpression, bool withServices, bool hasArguments)
        {
            ProcessResolveExpression(fieldExpression, withServices, hasArguments);
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