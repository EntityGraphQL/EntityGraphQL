using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Directives
{

    /// <summary>
    /// Base directive processor. To implement custom directives inherit from this class and override either or both
    /// ProcessQuery() - used to make changes to the query before execution (e.g. @include/skip)
    /// ProcessResult() - used to make changes to the result of the item the directive is on
    /// </summary>
    public abstract class DirectiveProcessor<TArguments> : IDirectiveProcessor
    {
        public Type GetArgumentsType() => typeof(TArguments);
        public abstract string Name { get; }
        public abstract string Description { get; }

        /// <summary>
        /// Implement this to make changes to the expression that will execute
        /// </summary>
        /// <param name="value"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public virtual Expression? ProcessExpression(Expression expression, object arguments)
        {
            // default return the value
            return expression;
        }

        /// <summary>
        /// Implement this to make changes to the field before the expressions are built
        /// </summary>
        /// <param name="field"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public virtual BaseGraphQLField? ProcessField(BaseGraphQLField field, object arguments)
        {
            return field;
        }

        public IEnumerable<ArgType> GetArguments(ISchemaProvider schema)
        {
            return GetArgumentsType().GetProperties().ToList().Select(prop => ArgType.FromProperty(schema, prop, null, schema.SchemaFieldNamer)).ToList();
        }
    }
}