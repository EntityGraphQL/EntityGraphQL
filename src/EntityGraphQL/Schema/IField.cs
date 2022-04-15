using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Schema
{
    public enum FieldType
    {
        Query,
        Mutation,
    }
    /// <summary>
    /// Represents a field in a GraphQL type. This can be a mutation field in the Mutation type or a field on a query type
    /// </summary>
    public interface IField
    {
        FieldType FieldType { get; }
        ISchemaProvider Schema { get; }
        ParameterExpression? FieldParam { get; set; }
        string? Description { get; }
        IDictionary<string, ArgType> Arguments { get; }
        ParameterExpression? ArgumentParam { get; }
        Type? ArgumentsType { get; set; }
        string Name { get; }
        GqlTypeInfo ReturnType { get; }
        List<IFieldExtension> Extensions { get; set; }
        RequiredAuthorization? RequiredAuthorization { get; }

        bool IsDeprecated { get; set; }
        string? DeprecationReason { get; set; }

        ArgType GetArgumentType(string argName);
        bool HasArgumentByName(string argName);
        bool ArgumentsAreInternal { get; }
        IEnumerable<Type> Services { get; }
        IField? UseArgumentsFromField { get; }

        /// <summary>
        /// Given the current context, a type and a field name, it returns the expression for that field. Allows the provider to have a complex expression for a simple field
        /// </summary>
        /// <param name="context"></param>
        /// <param name="previousContext"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        (Expression? expression, object? argumentValues) GetExpression(Expression fieldExpression, Expression? fieldContext, IGraphQLNode? parentNode, ParameterExpression? schemaContext, Dictionary<string, object> args, ParameterExpression? docParam, object? docVariables, IEnumerable<GraphQLDirective> directives, bool contextChanged);
        Expression? ResolveExpression { get; }

        IField UpdateExpression(Expression expression);

        void AddExtension(IFieldExtension extension);
        void AddArguments(object args);
        IField Returns(GqlTypeInfo gqlTypeInfo);
        void UseArgumentsFrom(IField edgesField);
    }
}