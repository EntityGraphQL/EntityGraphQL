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
            var allArgs = args.ToList();
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
                    if (key.ToLower() == prop.Name.ToLower())
                    {
                        object value = GetValue(gqlRequestArgs, prop, prop.PropertyType);
                        prop.SetValue(argInstance, value);
                        foundProp = true;
                    }
                }
                if (!foundProp)
                {
                    foreach (var field in argType.GetFields())
                    {
                        if (key.ToLower() == field.Name.ToLower())
                        {
                            object value = GetValue(gqlRequestArgs, field, field.FieldType);
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

        private object GetValue(Dictionary<string, ExpressionResult> gqlRequestArgs, MemberInfo member, Type memberType)
        {
            object value = Expression.Lambda(gqlRequestArgs[member.Name]).Compile().DynamicInvoke();
            if (value != null)
            {
                Type type = value.GetType();
                if (type.IsArray && memberType.IsEnumerable())
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

        public bool IsEnumerable => ReturnType.ContextType.IsEnumerable();

        public IDictionary<string, Type> Arguments => argumentTypes;

        public MutationType(string methodName, ISchemaType returnType, object mutationClassInstance, MethodInfo method, string description)
        {
            this.Description = description;
            this.returnType = returnType;
            this.mutationClassInstance = mutationClassInstance;
            this.method = method;
            Name = methodName;

            var methodArg = method.GetParameters().ElementAt(1);
            this.argInstance = Activator.CreateInstance(methodArg.ParameterType);
            foreach (var item in this.argInstance.GetType().GetProperties())
            {
                argumentTypes.Add(item.Name, item.PropertyType);
            }
            foreach (var item in this.argInstance.GetType().GetFields())
            {
                argumentTypes.Add(item.Name, item.FieldType);
            }
        }

        public Field GetField(string identifier, params string[] arguments)
        {
            return ReturnType.GetField(identifier, arguments);
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