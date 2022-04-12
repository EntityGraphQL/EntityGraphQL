using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Schema
{
    public abstract class BaseField : IField
    {
        public abstract FieldType FieldType { get; }
        public ParameterExpression? FieldParam { get; set; }

        public string Description { get; protected set; }
        public IDictionary<string, ArgType> Arguments { get; set; } = new Dictionary<string, ArgType>();
        public ParameterExpression? ArgumentParam { get; set; }
        public string Name { get; internal set; }
        public GqlTypeInfo ReturnType { get; protected set; }
        public List<IFieldExtension> Extensions { get; set; }
        public RequiredAuthorization? RequiredAuthorization { get; protected set; }
        public bool IsDeprecated { get; set; }
        public string? DeprecationReason { get; set; }

        /// <summary>
        /// If true the arguments on the field are used internally for processing (usually in extensions that change the 
        /// shape of the schema and need arguments from the original field)
        /// Arguments will not be in introspection
        /// </summary>
        public bool ArgumentsAreInternal { get; internal set; }

        /// <summary>
        /// Services required to be injected for this fields selection
        /// </summary>
        /// <value></value>
        public IEnumerable<Type> Services { get; set; } = new List<Type>();

        public Expression? Resolve { get; protected set; }

        public ISchemaProvider Schema { get; set; }
        public Type? ArgumentsType { get; set; }

        protected BaseField(ISchemaProvider schema, string name, string? description, GqlTypeInfo returnType)
        {
            this.Schema = schema;
            Description = description ?? string.Empty;
            Name = name;
            ReturnType = returnType;
            Extensions = new List<IFieldExtension>();
        }

        /// <summary>
        /// Add a field extension to this field
        /// </summary>
        /// <param name="extension"></param>
        public void AddExtension(IFieldExtension extension)
        {
            Extensions.Add(extension);
            extension.Configure(Schema, this);
        }

        public ArgType GetArgumentType(string argName)
        {
            return Arguments[argName];
        }

        public abstract ExpressionResult? GetExpression(Expression fieldExpression, Expression? fieldContext, IGraphQLNode? parentNode, ParameterExpression? schemaContext, Dictionary<string, object> args, ParameterExpression? docParam, object? docVariables, IEnumerable<GraphQLDirective> directives, bool contextChanged);
        public bool HasArgumentByName(string argName)
        {
            return Arguments.ContainsKey(argName);
        }

        /// <summary>
        /// Update the expression used to resolve this fields value
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public IField UpdateExpression(Expression expression)
        {
            Resolve = expression;
            return this;
        }

        /// <summary>
        /// Adds a argument object to the field. The fields on the object will be added as arguments.
        /// Any exisiting arguments with the same name will be overwritten.
        /// </summary>
        /// <param name="args"></param>
        public void AddArguments(object args)
        {
            // get new argument values
            var newArgs = ExpressionUtil.ObjectToDictionaryArgs(Schema, args, Schema.SchemaFieldNamer);
            // build new argument Type
            var newArgType = ExpressionUtil.MergeTypes(ArgumentsType, args.GetType());
            // Update the values - we don't read new values from this as the type has now lost any default values etc but we have them in allArguments
            newArgs.ToList().ForEach(k => Arguments.Add(k.Key, k.Value));
            // now we need to update the MemberInfo
            foreach (var item in Arguments)
            {
                item.Value.MemberInfo = (MemberInfo)newArgType.GetProperty(item.Value.DotnetName) ??
                    newArgType.GetField(item.Value.DotnetName);
            }
            var parameterReplacer = new ParameterReplacer();

            var argParam = Expression.Parameter(newArgType, $"arg_{newArgType.Name}");
            if (ArgumentParam != null && Resolve != null)
                Resolve = parameterReplacer.Replace(Resolve, ArgumentParam, argParam);

            ArgumentParam = argParam;
            ArgumentsType = newArgType;
        }
        public IField Returns(GqlTypeInfo gqlTypeInfo)
        {
            ReturnType = gqlTypeInfo;
            return this;
        }
    }
}