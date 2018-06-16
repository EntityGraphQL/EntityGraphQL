using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace EntityQueryLanguage.Schema
{
    /// Describes an entity field. It's expression based on the base type (your data model) and it's mapped return type
    public class Field
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
            var arg = ArgumentTypes.GetType().GetTypeInfo().GetProperties().Where(f => f.Name.ToLower() == argName.ToLower()).FirstOrDefault();
            if (arg == null)
            {
                throw new EqlCompilerException($"{argName} is not an argument on field {Name}");
            }
            return arg.PropertyType;
        }
    }
}