using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema.Directives;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Schema
{
    public enum GraphQLQueryFieldType
    {
        Query,
        Mutation,
        Subscription,
    }
    /// <summary>
    /// Represents a field in a GraphQL type. This can be a mutation field in the Mutation type or a field on a query type
    /// </summary>
    public interface IField
    {
        GraphQLQueryFieldType FieldType { get; }
        ISchemaProvider Schema { get; }
        ParameterExpression? FieldParam { get; set; }
        List<GraphQLExtractedField>? ExtractedFieldsFromServices { get; }
        string? Description { get; }
        /// <summary>
        /// Information about each field argument as represented in the GraphQL schema. 
        /// This is used to map the schema arguments to the dotnet expression arguments
        /// </summary>
        IDictionary<string, ArgType> Arguments { get; }
        /// <summary>
        /// This is a ParameterExpression that is used to access all the field's arguments in the field expression. 
        /// The type is a type that has all the field's GraphQL Schema arguments as properties.
        /// E.g. if the field has arguments (a, b, c) then expressions access them them like (args) => args.a + args.b + args.c
        /// Note that these instances are replaced within the expression at execution time. 
        /// You should not store these at configuration time in field extensions
        /// </summary>
        ParameterExpression? ArgumentsParameter { get; }
        /// <summary>
        /// This is the Type used in the field's expression. It maps to the arguments of the field.
        /// </summary>
        Type? ExpressionArgumentType { get; }
        string Name { get; }
        /// <summary>
        /// GraphQL type this field belongs to
        /// </summary>
        ISchemaType FromType { get; }
        /// <summary>
        /// Information about the GraphQL type returned by this field
        /// </summary>
        GqlTypeInfo ReturnType { get; }
        List<IFieldExtension> Extensions { get; set; }
        RequiredAuthorization? RequiredAuthorization { get; }

        IList<ISchemaDirective> DirectivesReadOnly { get; }
        IField AddDirective(ISchemaDirective directive);
        ArgType GetArgumentType(string argName);
        bool HasArgumentByName(string argName);
        /// <summary>
        /// If true the arguments on the field are used internally for processing (usually in extensions that change the 
        /// shape of the schema and need arguments from the original field)
        /// Arguments will not be in introspection
        /// </summary>
        bool ArgumentsAreInternal { get; }
        /// <summary>
        /// Services required to be injected for this fields selection
        /// </summary>
        IEnumerable<ParameterExpression> Services { get; }
        IReadOnlyCollection<Action<ArgumentValidatorContext>> Validators { get; }
        IField? UseArgumentsFromField { get; }

        /// <summary>
        /// Given the current context, a type and a field name, it returns the expression for that field. Allows the provider to have a complex expression for a simple field
        /// </summary>
        /// <param name="context"></param>
        /// <param name="previousContext"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        (Expression? expression, ParameterExpression? argumentParam) GetExpression(Expression fieldExpression, Expression? fieldContext, IGraphQLNode? parentNode, ParameterExpression? schemaContext, CompileContext? compileContext, IReadOnlyDictionary<string, object> args, ParameterExpression? docParam, object? docVariables, IEnumerable<GraphQLDirective> directives, bool contextChanged, ParameterReplacer replacer);
        Expression? ResolveExpression { get; }

        IField UpdateExpression(Expression expression);

        IField AddExtension(IFieldExtension extension);
        /// <summary>
        /// Add new arguments to the field. Properties on the args object will be merged with any existing arguments on the field.
        /// </summary>
        /// <param name="args"></param>
        void AddArguments(object args);
        IField Returns(GqlTypeInfo gqlTypeInfo);
        void UseArgumentsFrom(IField field);
        IField AddValidator<TValidator>() where TValidator : IArgumentValidator;
        IField AddValidator(Action<ArgumentValidatorContext> callback);

        /// <summary>
        /// To access this field all roles listed here are required
        /// </summary>
        /// <param name="roles"></param>
        IField RequiresAllRoles(params string[] roles);
        /// <summary>
        /// To access this field any role listed is required
        /// </summary>
        /// <param name="roles"></param>
        IField RequiresAnyRole(params string[] roles);
        /// <summary>
        /// To access this field all policies listed here are required
        /// </summary>
        /// <param name="policies"></param>
        IField RequiresAllPolicies(params string[] policies);
        /// <summary>
        /// To access this field any policy listed is required
        /// </summary>
        /// <param name="policies"></param>
        IField RequiresAnyPolicy(params string[] policies);
    }
}