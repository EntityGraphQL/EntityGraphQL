using System;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions
{
    /// <summary>
    /// Sets up a few extensions to modify a simple collection expression - db.Movies.OrderBy() into a connection paging graph
    /// </summary>
    public class OffsetPagingExtension : BaseFieldExtension
    {
        private IField itemsField;
        private MethodCallExpression itemsFieldExp;
        private readonly int? defaultPageSize;
        private readonly int? maxPageSize;

        public OffsetPagingExtension(int? defaultPageSize, int? maxPageSize)
        {
            this.defaultPageSize = defaultPageSize;
            this.maxPageSize = maxPageSize;
        }

        /// <summary>
        /// Configure the field for a offset style paging field. Do as much as we can here as it is only executed once.
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="field"></param>
        public override void Configure(ISchemaProvider schema, Field field)
        {
            if (!field.Resolve.Type.IsEnumerableOrArray())
                throw new ArgumentException($"Expression for field {field.Name} must be a collection to use OffsetPagingExtension. Found type {field.ReturnType.TypeDotnet}");

            Type listType = field.ReturnType.TypeDotnet.GetEnumerableOrArrayType();

            ISchemaType returnSchemaType;
            var page = $"{field.ReturnType.SchemaType.Name}Page";
            if (!schema.HasType(page))
            {
                var type = typeof(OffsetPage<>)
                    .MakeGenericType(listType);
                returnSchemaType = schema.AddType(type, page, $"Metadata about a {field.ReturnType.SchemaType.Name} page (paging over people)").AddAllFields();
            }
            else
            {
                returnSchemaType = schema.Type(page);
            }
            var returnType = returnSchemaType.TypeDotnet;

            field.UpdateReturnType(SchemaBuilder.MakeGraphQlType(schema, returnType, page));

            // Update field arguments
            field.AddArguments(new OffsetArgs());
            if (defaultPageSize.HasValue)
                field.Arguments["take"].DefaultValue = defaultPageSize.Value;

            var isQueryable = typeof(IQueryable).IsAssignableFrom(field.Resolve.Type);
            var totalCountExp = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Count", new Type[] { listType }, field.Resolve);

            // update the Items field before we update the field.Resolve below
            itemsField = schema.GetActualField(field.ReturnType.SchemaType.Name, "items", null);
            itemsFieldExp = Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), "Take", new Type[] { listType },
                Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), "Skip", new Type[] { listType },
                    field.Resolve,
                        Expression.PropertyOrField(field.ArgumentParam, "skip")
                ),
                Expression.PropertyOrField(field.ArgumentParam, "take")
            );
            itemsField.UpdateExpression(itemsFieldExp);

            var expression = Expression.MemberInit(
                Expression.New(returnType.GetConstructor(new[] { typeof(int), typeof(int?), typeof(int?) }), totalCountExp, Expression.PropertyOrField(field.ArgumentParam, "skip"), Expression.PropertyOrField(field.ArgumentParam, "take"))
            );

            field.UpdateExpression(expression);
        }

        public override Expression GetExpression(Field field, Expression expression, ParameterExpression argExpression, dynamic arguments, Expression context, ParameterReplacer parameterReplacer)
        {
            if (maxPageSize.HasValue && arguments.Take > maxPageSize.Value)
                throw new ArgumentException($"Argument take can not be greater than {maxPageSize.Value}.");
            // we have current context update Items field
            itemsField.UpdateExpression(
                parameterReplacer.Replace(itemsFieldExp, field.FieldParam, context)
            );

            return expression;
        }
    }
}