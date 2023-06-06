using System;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public class FilterExpressionExtension : BaseFieldExtension
    {
        private bool isQueryable;
        private Type? listType;

        /// <summary>
        /// Configure the field for a filter argument. Do as much as we can here as it is only executed once.
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="field"></param>
        public override void Configure(ISchemaProvider schema, IField field)
        {
            if (field.ResolveExpression == null)
                throw new EntityGraphQLCompilerException($"FilterExpressionExtension requires a Resolve function set on the field");

            if (!field.ResolveExpression.Type.IsEnumerableOrArray())
                throw new ArgumentException($"Expression for field {field.Name} must be a collection to use FilterExpressionExtension. Found type {field.ReturnType.TypeDotnet}");

            listType = field.ReturnType.TypeDotnet.GetEnumerableOrArrayType()!;

            // Update field arguments
            var args = Activator.CreateInstance(typeof(FilterArgs<>).MakeGenericType(listType))!;
            field.AddArguments(args);

            isQueryable = typeof(IQueryable).IsAssignableFrom(field.ResolveExpression.Type);
        }

        public override Expression? GetExpression(IField field, Expression expression, ParameterExpression? argumentParam, dynamic? arguments, Expression context, IGraphQLNode? parentNode, bool servicesPass, ParameterReplacer parameterReplacer)
        {
            // data is already filtered
            if (servicesPass)
                return expression;

            // we have current context update Items field
            if (arguments != null && arguments?.Filter != null && arguments?.Filter.HasValue)
            {
                expression = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Where", new Type[] { listType! }, expression, arguments!.Filter.Query);
            }

            return expression;
        }
    }
}