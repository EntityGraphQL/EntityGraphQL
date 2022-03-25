using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Represents a field in a GraphQL type. This can be a mutation field in the Mutation type or a field on a query type
    /// </summary>
    public interface IField
    {
        IDictionary<string, ArgType> Arguments { get; }
        ParameterExpression? ArgumentParam { get; }
        string Name { get; }
        GqlTypeInfo ReturnType { get; }
        List<IFieldExtension> Extensions { get; }
        RequiredAuthorization? RequiredAuthorization { get; }

        bool IsDeprecated { get; set; }
        string? DeprecationReason { get; set; }

        void Deprecate(string reason);

        ArgType GetArgumentType(string argName);

        /// <summary>
        /// Given the current context, a type and a field name, it returns the expression for that field. Allows the provider to have a complex expression for a simple field
        /// </summary>
        /// <param name="context"></param>
        /// <param name="typeName"></param>
        /// <param name="fieldName"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        ExpressionResult? GetExpression(Expression context, Dictionary<string, Expression>? args);

        IField UpdateExpression(Expression expression);
    }
}