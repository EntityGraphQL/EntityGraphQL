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
        private readonly ISchemaType returnType;
        private readonly object mutationClassInstance;
        private readonly MethodInfo method;
        private Dictionary<string, Type> argumentTypes = new Dictionary<string, Type>();
        private object argInstance;

        public Type ReturnTypeClr { get { return returnType.ContextType; } }

        public string Description { get; }

        public object Call(object[] args, Dictionary<string, ExpressionResult> gqlRequestArgs)
        {
            // first arg is the Context - required arg in the mutation method
            var allArgs = new List<object> { args.First() };

            // are they asking for any other args and do we have them
            var parameterInfo = method.GetParameters();
            foreach (var p in parameterInfo.Skip(1).Take(parameterInfo.Length - 2))
            {
                var match = args.FirstOrDefault(a => p.ParameterType.IsAssignableFrom(a.GetType()));
                if (match == null)
                {
                    throw new EntityGraphQLCompilerException($"Mutation {method.Name} expecting parameter {p.Name} of type {p.ParameterType}, but no arguments suuplied to GraphQL QueryObject of that type");
                }
                allArgs.Add(match);
            }

            // last arg is the arguments for the mutation - required as last arg in the mutation method
            AssignArgValues(gqlRequestArgs);
            allArgs.Add(argInstance);

            var result = method.Invoke(mutationClassInstance, allArgs.ToArray());
            return result;
        }

        private void AssignArgValues(Dictionary<string, ExpressionResult> gqlRequestArgs)
        {
            Type argType = argInstance.GetType();
            foreach (var key in gqlRequestArgs.Keys)
            {
                var foundProp = false;
                foreach (var prop in argType.GetProperties())
                {
                    if (key == prop.Name)
                    {
                        object value = GetValue(gqlRequestArgs, SchemaGenerator.ToCamelCaseStartsLower(prop.Name), prop.PropertyType);
                        prop.SetValue(argInstance, value);
                        foundProp = true;
                    }
                }
                if (!foundProp)
                {
                    foreach (var field in argType.GetFields())
                    {
                        if (key == field.Name)
                        {
                            object value = GetValue(gqlRequestArgs, SchemaGenerator.ToCamelCaseStartsLower(field.Name), field.FieldType);
                            field.SetValue(argInstance, value);
                            foundProp = true;
                        }
                    }
                }
                if (!foundProp)
                {
                    throw new EntityQuerySchemaError($"Could not find property or field {key} on in schema object {argType.Name}");
                }
            }
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
                    var arr = (Array)value;
                    var convertMethod = typeof(MutationType).GetMethod("ConvertArray", BindingFlags.NonPublic | BindingFlags.Static);
                    var generic = convertMethod.MakeGenericMethod(new[] {memberType.GetGenericArguments()[0]});
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

        public Type ContextType => ReturnType.ContextType;

        public string Name { get; }

        public ISchemaType ReturnType => returnType;

        public bool IsEnumerable => ReturnType.ContextType.IsEnumerableOrArray();

        public IDictionary<string, Type> Arguments => argumentTypes;

        public string ReturnTypeSingle => returnType.Name;

        public MutationType(string methodName, ISchemaType returnType, object mutationClassInstance, MethodInfo method, string description)
        {
            this.Description = description;
            this.returnType = returnType;
            this.mutationClassInstance = mutationClassInstance;
            this.method = method;
            Name = methodName;

            var methodArg = method.GetParameters().Last();
            this.argInstance = Activator.CreateInstance(methodArg.ParameterType);
            foreach (var item in this.argInstance.GetType().GetProperties())
            {
                argumentTypes.Add(SchemaGenerator.ToCamelCaseStartsLower(item.Name), item.PropertyType);
            }
            foreach (var item in this.argInstance.GetType().GetFields())
            {
                argumentTypes.Add(SchemaGenerator.ToCamelCaseStartsLower(item.Name), item.FieldType);
            }
        }

        public Field GetField(string identifier)
        {
            return ReturnType.GetField(identifier);
        }

        public bool HasArgumentByName(string argName)
        {
            return argumentTypes.ContainsKey(argName);
        }

        public Type GetArgumentType(string argName)
        {
            if (!argumentTypes.ContainsKey(argName))
            {
                throw new EntityQuerySchemaError($"Argument type not found for argument '{argName}'");
            }
            return argumentTypes[argName];
        }
    }
}