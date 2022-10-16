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
        public IField? UseArgumentsFromField { get; set; }

        public abstract GraphQLQueryFieldType FieldType { get; }
        public ParameterExpression? FieldParam { get; set; }

        public string Description { get; protected set; }
        public IDictionary<string, ArgType> Arguments { get; set; } = new Dictionary<string, ArgType>();
        public ParameterExpression? ArgumentParam { get; set; }
        public string Name { get; internal set; }
        public ISchemaType FromType { get; }
        public GqlTypeInfo ReturnType { get; protected set; }
        public List<IFieldExtension> Extensions { get; set; }
        public RequiredAuthorization? RequiredAuthorization { get; protected set; }
        protected List<ISchemaDirective> directives = new();
        public IList<ISchemaDirective> Directives => directives.AsReadOnly();

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

        public IReadOnlyCollection<Action<ArgumentValidatorContext>> Validators { get => argumentValidators; }

        public Expression? ResolveExpression { get; protected set; }

        public ISchemaProvider Schema { get; set; }
        public Type? ArgumentsType { get; set; }

        public List<GraphQLExtractedField>? ExtractedFieldsFromServices { get; protected set; }

        protected List<Action<ArgumentValidatorContext>> argumentValidators = new();

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
                        if (handler != null)
                        {
                            handler.ApplyExtension(this, attribute);
                        }
                    }
                }
            }
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

        /// <summary>
        /// Adds a argument object to the field. The fields on the object will be added as arguments.
        /// Any exisiting arguments with the same name will be overwritten.
        /// </summary>
        /// <param name="args"></param>
        public void AddArguments(object args)
        {
            // get new argument values
            var newArgs = ExpressionUtil.ObjectToDictionaryArgs(Schema, args);
            // build new argument Type
            var newArgType = ExpressionUtil.MergeTypes(ArgumentsType, args.GetType());
            // Update the values - we don't read new values from this as the type has now lost any default values etc but we have them in allArguments
            newArgs.ToList().ForEach(k => Arguments.Add(k.Key, k.Value));
          
            var parameterReplacer = new ParameterReplacer();

            var argParam = Expression.Parameter(newArgType, $"arg_{newArgType.Name}");
            if (ArgumentParam != null && ResolveExpression != null)
                ResolveExpression = parameterReplacer.Replace(ResolveExpression, ArgumentParam, argParam);

            ArgumentParam = argParam;
            ArgumentsType = newArgType;
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
            ArgumentsType = field.ArgumentsType;
            ArgumentParam = field.ArgumentParam;
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
            argumentValidators.Add((context) => validator.ValidateAsync(context));
            return this;
        }

        public IField AddValidator(Action<ArgumentValidatorContext> callback)
        {
            argumentValidators.Add(callback);
            return this;
        }
        public IField AddValidator(Func<ArgumentValidatorContext, Task> callback)
        {
            argumentValidators.Add((context) => callback(context).GetAwaiter().GetResult());
            return this;
        }

        public IField AddDirective(ISchemaDirective directive)
        {
            if (!directive.On.Any(x => x == TypeSystemDirectiveLocation.FIELD_DEFINITION))
                throw new InvalidOperationException($"{directive.GetType().Name} not valid on FIELD_DEFINITION");

            directives.Add(directive);
            return this;
        }

    }
}