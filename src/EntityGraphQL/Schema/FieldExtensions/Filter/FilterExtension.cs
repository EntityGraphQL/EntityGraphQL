using System;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public class FilterExtension : BaseFieldExtension
    {
        private bool isQueryable;
        private Type listType;

        /// <summary>
        /// Configure the field for a filter argument. Do as much as we can here as it is only executed once.
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="field"></param>
        public override void Configure(ISchemaProvider schema, Field field)
        {
            if (!field.Resolve.Type.IsEnumerableOrArray())
                throw new ArgumentException($"Expression for field {field.Name} must be a collection to use FilterExtension. Found type {field.ReturnType.TypeDotnet}");

            listType = field.ReturnType.TypeDotnet.GetEnumerableOrArrayType();

            // Update field arguments
            var args = Activator.CreateInstance(typeof(FilterArgs<>).MakeGenericType(listType));
            field.AddArguments(args);

            isQueryable = typeof(IQueryable).IsAssignableFrom(field.Resolve.Type);
        }

        public override Expression GetExpression(Field field, Expression expression, ParameterExpression argExpression, dynamic arguments, Expression context, ParameterReplacer parameterReplacer)
        {
            // we have current context update Items field
            if (arguments.filter != null && arguments.filter.HasValue)
                expression = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Where", new Type[] { listType }, expression, arguments.filter.Query);

            return expression;
        }
    }
}