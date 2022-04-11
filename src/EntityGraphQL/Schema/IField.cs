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
        string? Description { get; }
        IDictionary<string, ArgType> Arguments { get; }
        ParameterExpression? ArgumentParam { get; }
        string Name { get; }
        GqlTypeInfo ReturnType { get; }
        List<IFieldExtension> Extensions { get; }
        RequiredAuthorization? RequiredAuthorization { get; }

        bool IsDeprecated { get; set; }
        string? DeprecationReason { get; set; }

        ArgType GetArgumentType(string argName);
        bool HasArgumentByName(string argName);
        bool ArgumentsAreInternal { get; }
        IEnumerable<Type> Services { get; }

        /// <summary>
        /// Given the current context, a type and a field name, it returns the expression for that field. Allows the provider to have a complex expression for a simple field
        /// </summary>
        /// <param name="context"></param>
        /// <param name="previousContext"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        ExpressionResult GetExpression(Expression fieldExpression, Expression fieldContext, ParameterExpression? schemaContext, Dictionary<string, object> args, ParameterExpression? docParam, object? docVariables, bool contextChanged);
        Expression? Resolve { get; }

        IField UpdateExpression(Expression expression);

        void AddExtension(IFieldExtension extension);
    }
}