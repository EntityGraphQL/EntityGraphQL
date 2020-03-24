using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema
{
    public class MutationType : IMethodType
    {
        private readonly object mutationClassInstance;
        private readonly MethodInfo method;
        private readonly Dictionary<string, ArgType> argumentTypes = new Dictionary<string, ArgType>();
        private readonly Type argInstanceType;

        public Type ReturnTypeClr { get { return ReturnType.ContextType; } }

        public string Description { get; }

        public Type ContextType => ReturnType.ContextType;

        public string Name { get; }
        public RequiredClaims AuthorizeClaims { get; }

        public ISchemaType ReturnType { get; }

        public IDictionary<string, ArgType> Arguments => argumentTypes;

        public bool ReturnTypeNotNullable => false;
        public bool ReturnElementTypeNullable => false;

        public string GetReturnType(ISchemaProvider schema)
        {
            return ReturnType.Name;
        }

        public object Call(object context, Dictionary<string, ExpressionResult> gqlRequestArgs, IServiceProvider serviceProvider)
        {
            // first arg is the Context - required arg in the mutation method
            var allArgs = new List<object> { context };

            // second arg is the arguments for the mutation - required as last arg in the mutation method
            var argInstance = AssignArgValues(gqlRequestArgs);
            allArgs.Add(argInstance);

            // add any DI services
            foreach (var p in method.GetParameters().Skip(2))
            {
                var service = serviceProvider.GetService(p.ParameterType);
                if (service == null)
                {
                    throw new EntityGraphQLCompilerException($"Service {p.ParameterType.Name} not found for dependency injection for mutation {method.Name}");
                }
                allArgs.Add(service);
            }

            var result = method.Invoke(mutationClassInstance, allArgs.ToArray());
            return result;
        }

        private object AssignArgValues(Dictionary<string, ExpressionResult> gqlRequestArgs)
        {
            var argInstance = Activator.CreateInstance(this.argInstanceType);
            Type argType = this.argInstanceType;
            foreach (var key in gqlRequestArgs.Keys)
            {
                var foundProp = false;
                foreach (var prop in argType.GetProperties())
                {
                    var propName = SchemaGenerator.ToCamelCaseStartsLower(prop.Name);
                    if (key == propName)
                    {
                        object value = GetValue(gqlRequestArgs, propName, prop.PropertyType);
                        prop.SetValue(argInstance, value);
                        foundProp = true;
                    }
                }
                if (!foundProp)
                {
                    foreach (var field in argType.GetFields())
                    {
                        var fieldName = SchemaGenerator.ToCamelCaseStartsLower(field.Name);
                        if (key == fieldName)
                        {
                            object value = GetValue(gqlRequestArgs, fieldName, field.FieldType);
                            field.SetValue(argInstance, value);
                            foundProp = true;
                        }
                    }
                }
                if (!foundProp)
                {
                    throw new EntityQuerySchemaException($"Could not find property or field {key} on schema object {argType.Name}");
                }
            }
            return argInstance;
        }

        /// <summary>
        /// Used at runtime below
        /// </summary>
        /// <param name="input"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private static List<T> ConvertArray<T>(Array input)
        {
            return input.Cast<T>().ToList(); // Using LINQ for simplicity
        }

        private object GetValue(Dictionary<string, ExpressionResult> gqlRequestArgs, string memberName, Type memberType)
        {
            object value = Expression.Lambda(gqlRequestArgs[memberName]).Compile().DynamicInvoke();
            if (value != null)
            {
                Type type = value.GetType();
                if (type.IsArray && memberType.IsEnumerableOrArray())
                {
                    var convertMethod = typeof(MutationType).GetMethod("ConvertArray", BindingFlags.NonPublic | BindingFlags.Static);
                    var generic = convertMethod.MakeGenericMethod(new[] { memberType.GetGenericArguments()[0] });
                    value = generic.Invoke(null, new object[] { value });
                }
                else if (type == typeof(Newtonsoft.Json.Linq.JObject))
                {
                    value = ((Newtonsoft.Json.Linq.JObject)value).ToObject(memberType);
                }
                else
                {
                    value = ExpressionUtil.ChangeType(value, memberType);
                }
            }
            return value;
        }

        public MutationType(string methodName, ISchemaType returnType, object mutationClassInstance, MethodInfo method, string description, RequiredClaims authorizeClaims)
        {
            this.Description = description;
            this.ReturnType = returnType;
            this.mutationClassInstance = mutationClassInstance;
            this.method = method;
            Name = methodName;
            AuthorizeClaims = authorizeClaims;

            var methodArg = method.GetParameters().ElementAt(1);
            this.argInstanceType = methodArg.ParameterType;
            foreach (var item in argInstanceType.GetProperties())
            {
                if (GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(item))
                    continue;
                argumentTypes.Add(SchemaGenerator.ToCamelCaseStartsLower(item.Name), new ArgType
                {
                    Type = item.PropertyType,
                    TypeNotNullable = GraphQLNotNullAttribute.IsMemberMarkedNotNull(item) || item.PropertyType.GetTypeInfo().IsEnum
                });
            }
            foreach (var item in argInstanceType.GetFields())
            {
                if (GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(item))
                    continue;
                argumentTypes.Add(SchemaGenerator.ToCamelCaseStartsLower(item.Name), new ArgType
                {
                    Type = item.FieldType,
                    TypeNotNullable = GraphQLNotNullAttribute.IsMemberMarkedNotNull(item) || item.FieldType.GetTypeInfo().IsEnum
                });
            }
        }

        public bool HasArgumentByName(string argName)
        {
            return argumentTypes.ContainsKey(argName);
        }

        public ArgType GetArgumentType(string argName)
        {
            if (!argumentTypes.ContainsKey(argName))
            {
                throw new EntityQuerySchemaException($"Argument type not found for argument '{argName}'");
            }
            return argumentTypes[argName];
        }
    }
}