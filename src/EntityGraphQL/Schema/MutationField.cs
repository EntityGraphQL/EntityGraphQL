using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Schema
{
    public class MutationField : BaseField
    {
        public override FieldType FieldType { get; } = FieldType.Mutation;
        private readonly object mutationClassInstance;
        private readonly MethodInfo method;
        private readonly bool isAsync;

        public MutationField(ISchemaProvider schema, string methodName, GqlTypeInfo returnType, object mutationClassInstance, MethodInfo method, string description, RequiredAuthorization requiredAuth, bool isAsync, Func<string, string> fieldNamer)
            : base(schema, methodName, description, returnType)
        {
            Services = new List<Type>();
            this.mutationClassInstance = mutationClassInstance;
            this.method = method;
            RequiredAuthorization = requiredAuth;
            this.isAsync = isAsync;

            ArgumentsType = method.GetParameters()
                .FirstOrDefault(p => p.GetCustomAttribute(typeof(MutationArgumentsAttribute)) != null || p.ParameterType.GetTypeInfo().GetCustomAttribute(typeof(MutationArgumentsAttribute)) != null)?.ParameterType;
            if (ArgumentsType != null)
            {
                foreach (var item in ArgumentsType.GetProperties())
                {
                    if (GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(item))
                        continue;
                    Arguments.Add(fieldNamer(item.Name), ArgType.FromProperty(schema, item, null, fieldNamer));
                }
                foreach (var item in ArgumentsType.GetFields())
                {
                    if (GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(item))
                        continue;
                    Arguments.Add(fieldNamer(item.Name), ArgType.FromField(schema, item, null, fieldNamer));
                }
            }
        }

        public void Deprecate(string reason)
        {
            IsDeprecated = true;
            DeprecationReason = reason;
        }

        public async Task<object?> CallAsync(object? context, Dictionary<string, object>? gqlRequestArgs, GraphQLValidator validator, IServiceProvider serviceProvider, ParameterExpression? variableParameter, object? docVariables, Func<string, string> fieldNamer)
        {
            if (context == null)
                return null;

            // args in the mutation method
            var allArgs = new List<object>();

            if (gqlRequestArgs?.Count > 0)
            {
                var argInstance = BuildArgumentsObject(gqlRequestArgs, this, variableParameter, docVariables);
                VaildateModelBinding(argInstance, validator);
                if (validator.Errors.Any())
                    return null;

                // add parameters and any DI services
                foreach (var p in method.GetParameters())
                {
                    if (p.GetCustomAttribute(typeof(MutationArgumentsAttribute)) != null || p.ParameterType.GetTypeInfo().GetCustomAttribute(typeof(MutationArgumentsAttribute)) != null)
                    {
                        allArgs.Add(argInstance);
                    }
                    else if (p.ParameterType == context.GetType())
                    {
                        allArgs.Add(context);
                    }
                    // todo we should put this in the IServiceCollection actually...
                    else if (p.ParameterType == typeof(GraphQLValidator))
                    {
                        allArgs.Add(validator);
                    }
                    else
                    {
                        var service = serviceProvider.GetService(p.ParameterType);
                        if (service == null)
                        {
                            throw new EntityGraphQLExecutionException($"Service {p.ParameterType.Name} not found for dependency injection for mutation {method.Name}");
                        }
                        allArgs.Add(service);
                    }
                }
            }

            object result;
            if (isAsync)
            {
                result = await (dynamic)method.Invoke(mutationClassInstance, allArgs.ToArray());
            }
            else
            {
                try
                {
                    result = method.Invoke(mutationClassInstance, allArgs.ToArray());
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException != null)
                        throw ex.InnerException;
                    throw;
                }
            }
            return result;
        }

        /// <summary>
        /// Used at runtime below!!
        /// </summary>
        /// <param name="input"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private static List<T> ConvertArray<T>(Array input)
        {
            return input.Cast<T>().ToList(); // Using LINQ for simplicity
        }

        private void VaildateModelBinding(object entity, GraphQLValidator validator)
        {
            Type argType = entity.GetType();
            foreach (var prop in argType.GetProperties())
            {
                object value = prop.GetValue(entity, null);

                // set default message in-case user didn't provide a custom one
                string error = $"{prop.Name} is required";

                if (validator.Errors.Any(x => x.Message == error))
                    return;

                if (prop.GetCustomAttribute(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute)) is System.ComponentModel.DataAnnotations.RequiredAttribute attr)
                {
                    if (attr.ErrorMessage != null)
                        error = attr.ErrorMessage;

                    if (value == null)
                        validator.AddError(error);
                    else if (!attr.AllowEmptyStrings && prop.PropertyType == typeof(string) && ((string)value).Length == 0)
                        validator.AddError(error);
                }
            }
        }

        public override ExpressionResult GetExpression(Expression fieldExpression, Expression? fieldContext, IGraphQLNode? parentNode, ParameterExpression? schemaContext, Dictionary<string, object> args, ParameterExpression? docParam, object? docVariables, bool contextChanged)
        {
            var result = (ExpressionResult)fieldExpression;

            if (schemaContext != null)
            {
                var parameterReplacer = new ParameterReplacer();
                result.Expression = parameterReplacer.ReplaceByType(result.Expression, schemaContext.Type, schemaContext);
            }
            return result;
        }
    }
}