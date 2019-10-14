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
        private readonly Dictionary<string, Type> allArguments = new Dictionary<string, Type>();

        public string Name { get; internal set; }
        public ParameterExpression FieldParam { get; private set; }
        internal Field(string name, LambdaExpression resolve, string description, string returnSchemaType = null)
        {
            Name = name;
            Resolve = resolve.Body;
            Description = description;
            FieldParam = resolve.Parameters.First();
            ReturnTypeSingle = returnSchemaType;
            IsEnumerable = resolve.Body.Type.IsEnumerableOrArray();
            if (ReturnTypeSingle == null)
            {
                if (IsEnumerable)
                {
                    if (!resolve.Body.Type.IsArray && !resolve.Body.Type.GetGenericArguments().Any())
                    {
                        throw new ArgumentException($"We think {resolve.Body.Type} is IEnumerable<> or an array but didn't find it's enumerable type");
                    }
                    ReturnTypeSingle = resolve.Body.Type.GetEnumerableOrArrayType().Name;
                }
                else
                {
                    ReturnTypeSingle = resolve.Body.Type.Name;
                }
            }
        }

        public Field(string name, LambdaExpression resolve, string description, string returnSchemaType, object argTypes) : this(name, resolve, description, returnSchemaType)
        {
            this.ArgumentTypesObject = argTypes;
            this.allArguments = argTypes.GetType().GetProperties().ToDictionary(p => p.Name, p => p.PropertyType);
            argTypes.GetType().GetFields().ToDictionary(p => p.Name, p => p.FieldType).ToList().ForEach(kvp => allArguments.Add(kvp.Key, kvp.Value));
        }

        public Expression Resolve { get; private set; }
        public string Description { get; private set; }
        public string ReturnTypeSingle { get; private set; }

        public bool IsEnumerable { get; }

        public object ArgumentTypesObject { get; private set; }
        public IDictionary<string, Type> Arguments { get { return allArguments; } }

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

        public Type ReturnTypeClr { get { return Resolve.Type; } }

        public bool HasArgumentByName(string argName)
        {
            return allArguments.ContainsKey(argName);
        }

        public Type GetArgumentType(string argName)
        {
            return allArguments[argName];
        }
    }
}