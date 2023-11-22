using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Respresents a GraphQL field backed by a method in dotnet. This is used for "controller" type fields. e.g. Mutations and Subscriptions.
    /// This is not used for query fields.
    /// 
    /// The way we execute this field is different to a query field with is built inline to an expression.
    /// 
    /// A MethodField is the top level field in a mutation/subscription and maps to a dotnet method. The result is projected back into the query graph.
    /// </summary>
    public abstract class MethodField : BaseField
    {
        public override GraphQLQueryFieldType FieldType { get; }
        protected MethodInfo Method { get; set; }
        public bool IsAsync { get; protected set; }

        public MethodField(ISchemaProvider schema, ISchemaType fromType, string methodName, GqlTypeInfo returnType, MethodInfo method, string description, RequiredAuthorization requiredAuth, bool isAsync, SchemaBuilderOptions options)
            : base(schema, fromType, methodName, description, returnType)
        {
            Method = method;
            RequiredAuthorization = requiredAuth;
            IsAsync = isAsync;
            Dictionary<string, Type> flattenedTypes;
            foreach (var item in SchemaBuilder.GetGraphQlSchemaArgumentsFromMethod(schema, method, options, out flattenedTypes))
            {
                if (item.IsService)
                    // services are not arguments in the schema
                    // We do not add service to BaseField.Services as this MethodField is used for mutations/subscriptions
                    // that make this method call then make a query on the result.
                    // BaseField.Services are for fields in a query result
                    // Services will be injected for us below in CallAsync
                    continue;

                if (item.ShouldFlatten)
                {
                    foreach (var f in item.FlattenArgs!)
                    {
                        Arguments.Add(f.ArgName, f.ArgType!);
                    }
                }
                else
                    Arguments.Add(item.ArgName, item.ArgType!);
            }
            ExpressionArgumentType = LinqRuntimeTypeBuilder.GetDynamicType(flattenedTypes, method.Name)!;
        }

        public virtual async Task<object?> CallAsync(object? context, IReadOnlyDictionary<string, object>? gqlRequestArgs, IServiceProvider? serviceProvider, ParameterExpression? variableParameter, object? docVariables)
        {
            if (context == null)
                return null;

            // args in the mutation method - may be arguments in the graphql schema, services injected
            var allArgs = new List<object>();
            var argsToValidate = new List<object>();
            object? argInstance = null;
            var validationErrors = new List<string>();

            // add parameters and any DI services
            foreach (var p in Method.GetParameters())
            {
                if (p.GetCustomAttribute<GraphQLArgumentsAttribute>() != null || p.ParameterType.GetTypeInfo().GetCustomAttribute<GraphQLArgumentsAttribute>() != null)
                {
                    // need to map GQL args to the GraphQLArguments object - p.ParameterType
                    argInstance = ArgumentUtil.BuildArgumentsObject(Schema, Name, this, gqlRequestArgs ?? new Dictionary<string, object>(), Arguments.Values, p.ParameterType, variableParameter, docVariables, validationErrors)!;
                    allArgs.Add(argInstance);
                }
                else if (gqlRequestArgs != null && gqlRequestArgs.ContainsKey(p.Name!))
                {
                    var argField = Arguments[p.Name!];
                    var value = ArgumentUtil.BuildArgumentFromMember(Schema, gqlRequestArgs, argField.Name, argField.RawType, argField.DefaultValue, validationErrors);
                    if (docVariables != null)
                    {
                        if (value is Expression and not null)
                        {
                            value = Expression.Lambda((Expression)value, variableParameter!).Compile().DynamicInvoke(new[] { docVariables });
                        }
                    }
                    // this could be int to RequiredField<int>
                    if (value != null && value.GetType() != argField.RawType)
                    {
                        value = ExpressionUtil.ChangeType(value, argField.RawType, Schema);
                    }

                    argField.Validate(value, p.Name!, validationErrors);

                    allArgs.Add(value!);
                    argsToValidate.Add(value!);
                }
                else if (p.ParameterType == context.GetType())
                {
                    allArgs.Add(context);
                }
                else if (serviceProvider != null)
                {
                    var service = serviceProvider.GetService(p.ParameterType) ?? throw new EntityGraphQLExecutionException($"Service {p.ParameterType.Name} not found for dependency injection for mutation {Method.Name}");
                    allArgs.Add(service);
                }
                else
                {
                    var argField = Arguments[p.Name!];
                    if (argField.DefaultValue != null)
                    {
                        allArgs.Add(argField.DefaultValue);
                    }
                }
            }

            if (ArgumentValidators.Count > 0)
            {
                var validatorContext = new ArgumentValidatorContext(this, argInstance ?? argsToValidate, Method);
                foreach (var argValidator in ArgumentValidators)
                {
                    argValidator(validatorContext);
                }
                if (validatorContext.Errors != null && validatorContext.Errors.Count > 0)
                {
                    validationErrors.AddRange(validatorContext.Errors);
                }
            }

            if (validationErrors.Count > 0)
            {
                throw new EntityGraphQLValidationException(validationErrors);
            }

            object? instance = null;
            // we create an instance _per request_ injecting any parameters to the constructor
            // We kind of treat a mutation class like an asp.net controller
            // and we do not want to register them in the service provider to avoid the same issues controllers would have
            // with different lifetime objects
            if (instance == null)
            {
                instance = serviceProvider != null ?
                    ActivatorUtilities.CreateInstance(serviceProvider, Method.DeclaringType!) :
                    Activator.CreateInstance(Method.DeclaringType!);
            }

            object? result;
            if (IsAsync)
            {
                result = await (dynamic?)Method.Invoke(instance, allArgs.Any() ? allArgs.ToArray() : null);
            }
            else
            {
                try
                {
                    result = Method.Invoke(instance, allArgs.ToArray());
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

        public override (Expression? expression, ParameterExpression? argumentParam) GetExpression(Expression fieldExpression, Expression? fieldContext, IGraphQLNode? parentNode, ParameterExpression? schemaContext, CompileContext? compileContext, IReadOnlyDictionary<string, object> args, ParameterExpression? docParam, object? docVariables, IEnumerable<GraphQLDirective> directives, bool contextChanged, ParameterReplacer replacer)
        {
            var result = fieldExpression;

            if (schemaContext != null && result != null)
            {
                result = replacer.ReplaceByType(result, schemaContext.Type, schemaContext);
            }
            return (result, null);
        }
    }
}