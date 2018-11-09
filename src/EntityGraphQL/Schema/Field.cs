using System;
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
            this.Resolve = resolve.Body;
        }

        public Expression Resolve { get; private set; }
        public string Description { get; private set; }
        public string ReturnSchemaType { get; private set; }
        public object ArgumentTypes { get; private set; }

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