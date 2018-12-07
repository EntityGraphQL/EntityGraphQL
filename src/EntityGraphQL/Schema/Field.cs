using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Compiler;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema
{
    /// Describes an entity field. It's expression based on the base type (your data model) and it's mapped return type
    public class Field : IMethodType
    {
        public string Key
        {
            get
            {
                return MakeFieldKey(Name, ArgumentNames);
            }
        }

        public static string MakeFieldKey(string name, IEnumerable<string> args)
        {
            return name + "|" + (args != null ? string.Join(".", args) : "");
        }

        public string Name { get; internal set; }
        public ParameterExpression FieldParam { get; private set; }
        internal Field(string name, LambdaExpression resolve, string description, string returnSchemaType = null)
        {
            Name = name;
            Resolve = resolve.Body;
            Description = description;
            FieldParam = resolve.Parameters.First();
            ReturnSchemaType = returnSchemaType;
            if (ReturnSchemaType == null)
            {
                if (resolve.Body.Type.IsEnumerable())
                {
                    ReturnSchemaType = resolve.Body.Type.GetGenericArguments()[0].Name;
                }
                else
                {
                    ReturnSchemaType = resolve.Body.Type.Name;
                }
            }
        }

        public Field(string name, LambdaExpression resolve, string description, string returnSchemaType, object argTypes) : this(name, resolve, description, returnSchemaType)
        {
            this.ArgumentTypes = argTypes;
            this.ArgumentNames = argTypes.GetType().GetProperties().Select(p => p.Name).Concat(argTypes.GetType().GetFields().Select(p => p.Name)).ToList();
            this.Resolve = resolve.Body;
        }

        public Expression Resolve { get; private set; }
        public string Description { get; private set; }
        public string ReturnSchemaType { get; private set; }
        public object ArgumentTypes { get; private set; }
        public IEnumerable<string> ArgumentNames { get; }
        public IEnumerable<string> RequiredArgumentNames
        {
            get
            {
                var required = ArgumentTypes.GetType().GetTypeInfo().GetFields().Where(f => f.FieldType.IsConstructedGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(RequiredField<>)).Select(f => f.Name);
                var requiredProps = ArgumentTypes.GetType().GetTypeInfo().GetProperties().Where(f => f.PropertyType.IsConstructedGenericType && f.PropertyType.GetGenericTypeDefinition() == typeof(RequiredField<>)).Select(f => f.Name);
                return required.Concat(requiredProps).ToList();
            }
        }

        public Type GetArgumentType(string argName)
        {
            var argProp = ArgumentTypes.GetType().GetTypeInfo().GetProperties().Where(f => f.Name.ToLower() == argName.ToLower()).FirstOrDefault();
            if (argProp == null)
            {
                var argField = ArgumentTypes.GetType().GetTypeInfo().GetFields().Where(f => f.IsPublic && f.Name.ToLower() == argName.ToLower()).FirstOrDefault();
                if (argField == null)
                {
                    throw new EntityGraphQLCompilerException($"{argName} is not an argument on field {Name}");
                }
                return argField.FieldType;
            }
            return argProp.PropertyType;
        }
    }
}