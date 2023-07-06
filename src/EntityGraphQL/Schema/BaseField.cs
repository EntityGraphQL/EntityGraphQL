using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema.Directives;
using EntityGraphQL.Schema.FieldExtensions;
using EntityGraphQL.Schema.Validators;

namespace EntityGraphQL.Schema
{
    public abstract class BaseField : IField
    {
        #region IField properties
        public abstract GraphQLQueryFieldType FieldType { get; }
        public ISchemaProvider Schema { get; set; }
        public ParameterExpression? FieldParam { get; set; }
        public List<GraphQLExtractedField>? ExtractedFieldsFromServices { get; protected set; }
        public string Description { get; protected set; }
        public IDictionary<string, ArgType> Arguments { get; set; } = new Dictionary<string, ArgType>();
        public ParameterExpression? ArgumentsParameter { get; set; }
        public Type? ExpressionArgumentType { get; internal set; }
        public string Name { get; internal set; }
        public ISchemaType FromType { get; }
        public GqlTypeInfo ReturnType { get; protected set; }
        public List<IFieldExtension> Extensions { get; set; }
        public RequiredAuthorization? RequiredAuthorization { get; protected set; }
        public IList<ISchemaDirective> DirectivesReadOnly => Directives.AsReadOnly();
        public bool ArgumentsAreInternal { get; internal set; }
        public IEnumerable<ParameterExpression> Services { get; set; } = new List<ParameterExpression>();
        public IReadOnlyCollection<Action<ArgumentValidatorContext>> Validators { get => ArgumentValidators; }
        public IField? UseArgumentsFromField { get; set; }
        public Expression? ResolveExpression { get; protected set; }

        #endregion IField properties

        protected List<ISchemaDirective> Directives { get; set; } = new();
        protected List<Action<ArgumentValidatorContext>> ArgumentValidators { get; set; } = new();

        protected BaseField(ISchemaProvider schema, ISchemaType fromType, string name, string? description, GqlTypeInfo returnType)
        {
            this.Schema = schema;
            FromType = fromType;
            Description = description ?? string.Empty;
            Name = name;
            ReturnType = returnType;
            Extensions = new List<IFieldExtension>();
            AddValidator<DataAnnotationsValidator>();
        }

        public void ApplyAttributes(IEnumerable<Attribute> attributes)
        {
            if (attributes.Any())
            {
                foreach (var attribute in attributes)
                {
                    if (attribute is ArgumentValidatorAttribute validator)
                    {
                        AddValidator(validator.Validator.ValidateAsync);
                    }
                    else if (attribute is ExtensionAttribute extension)
                    {
                        extension.ApplyExtension(this);
                    }
                    else
                    {
                        var handler = Schema.GetAttributeHandlerFor(attribute.GetType());
                        handler?.ApplyExtension(this, attribute);
                    }
                }
            }
        }

        /// <summary>
        /// Add a field extension to this field 
        /// </summary>
        /// <param name="extension"></param>
        public IField AddExtension(IFieldExtension extension)
        {
            Extensions.Add(extension);
            extension.Configure(Schema, this);
            return this;
        }

        public ArgType GetArgumentType(string argName)
        {
            return Arguments[argName];
        }

        public abstract (Expression? expression, ParameterExpression? argumentParam) GetExpression(Expression fieldExpression, Expression? fieldContext, IGraphQLNode? parentNode, ParameterExpression? schemaContext, CompileContext? compileContext, IReadOnlyDictionary<string, object> args, ParameterExpression? docParam, object? docVariables, IEnumerable<GraphQLDirective> directives, bool contextChanged, ParameterReplacer replacer);
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
            ResolveExpression = expression;
            return this;
        }

        public void AddArguments(object args)
        {
            // get new argument values
            var newArgs = ExpressionUtil.ObjectToDictionaryArgs(Schema, args);
            // build a new type with the new arguments
            var newArgType = ExpressionUtil.MergeTypes(ExpressionArgumentType, args.GetType());
            // Update the values - we don't read new values from this as the type has now lost any default values etc but we have them in allArguments
            newArgs.ToList().ForEach(k => Arguments.Add(k.Key, k.Value));

            // now we need to update the MemberInfo
            foreach (var item in Arguments)
            {
                item.Value.MemberInfo = (MemberInfo?)newArgType.GetProperty(item.Value.DotnetName) ??
                    newArgType.GetField(item.Value.DotnetName);
            }
            var parameterReplacer = new ParameterReplacer();

            var argParam = Expression.Parameter(newArgType, $"arg_{newArgType.Name}");
            if (ArgumentsParameter != null && ResolveExpression != null)
                ResolveExpression = parameterReplacer.Replace(ResolveExpression, ArgumentsParameter, argParam);

            ArgumentsParameter = argParam;
            ExpressionArgumentType = newArgType;
        }
        public IField Returns(GqlTypeInfo gqlTypeInfo)
        {
            ReturnType = gqlTypeInfo;
            return this;
        }

        public void UseArgumentsFrom(IField field)
        {
            // Move the arguments definition to the new field as it needs them for processing
            // don't push field.FieldParam over 
            ExpressionArgumentType = field.ExpressionArgumentType;
            ArgumentsParameter = field.ArgumentsParameter;
            Arguments = field.Arguments;
            ArgumentsAreInternal = true;
            UseArgumentsFromField = field;
        }
        /// <summary>
        /// To access this field all roles listed here are required
        /// </summary>
        /// <param name="roles"></param>
        public IField RequiresAllRoles(params string[] roles)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAllRoles(roles);
            return this;
        }

        /// <summary>
        /// To access this field any role listed is required
        /// </summary>
        /// <param name="roles"></param>
        public IField RequiresAnyRole(params string[] roles)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAllRoles(roles);
            return this;
        }

        /// <summary>
        /// To access this field all policies listed here are required
        /// </summary>
        /// <param name="policies"></param>
        public IField RequiresAllPolicies(params string[] policies)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAllPolicies(policies);
            return this;
        }

        /// <summary>
        /// To access this field any policy listed is required
        /// </summary>
        /// <param name="policies"></param>
        public IField RequiresAnyPolicy(params string[] policies)
        {
            if (RequiredAuthorization == null)
                RequiredAuthorization = new RequiredAuthorization();
            RequiredAuthorization.RequiresAnyPolicy(policies);
            return this;
        }

        /// <summary>
        /// Clears any authorization requirements for this field
        /// </summary>
        /// <returns></returns>
        public IField ClearAuthorization()
        {
            RequiredAuthorization = null;
            return this;
        }

        public IField AddValidator<TValidator>() where TValidator : IArgumentValidator
        {
            var validator = (IArgumentValidator)Activator.CreateInstance<TValidator>();
            ArgumentValidators.Add((context) => validator.ValidateAsync(context));
            return this;
        }

        public IField AddValidator(Action<ArgumentValidatorContext> callback)
        {
            ArgumentValidators.Add(callback);
            return this;
        }
        public IField AddValidator(Func<ArgumentValidatorContext, Task> callback)
        {
            ArgumentValidators.Add((context) => callback(context).GetAwaiter().GetResult());
            return this;
        }

        public IField AddDirective(ISchemaDirective directive)
        {
            if (!directive.Location.Any(x => x == TypeSystemDirectiveLocation.FieldDefinition))
                throw new InvalidOperationException($"{directive.GetType().Name} not valid on FIELD_DEFINITION");

            Directives.Add(directive);
            return this;
        }

    }
}