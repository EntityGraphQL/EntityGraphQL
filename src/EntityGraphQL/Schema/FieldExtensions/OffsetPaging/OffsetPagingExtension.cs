using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions
{
    /// <summary>
    /// Sets up a few extensions to modify a simple collection expression - db.Movies.OrderBy() into a connection paging graph
    /// </summary>
    public class OffsetPagingExtension : BaseFieldExtension
    {
        private Field? itemsField;
        private Field? field;
        private List<IFieldExtension>? extensions;
        private bool isQueryable;
        private Type? listType;
        private Type? returnType;
        private Expression? fieldExpressionWithExtensionsApplied;
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
            if (field.Resolve == null)
                throw new EntityGraphQLCompilerException($"OffsetPagingExtension requires a Resolve function set on the field");

            if (!field.Resolve.Type.IsEnumerableOrArray())
                throw new ArgumentException($"Expression for field {field.Name} must be a collection to use OffsetPagingExtension. Found type {field.ReturnType.TypeDotnet}");

            listType = field.ReturnType.TypeDotnet.GetEnumerableOrArrayType();
            this.field = field;

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
            returnType = returnSchemaType.TypeDotnet;

            field.Returns(SchemaBuilder.MakeGraphQlType(schema, returnType, page));

            // Update field arguments
            field.AddArguments(new OffsetArgs());
            if (defaultPageSize.HasValue)
                field.Arguments["take"].DefaultValue = defaultPageSize.Value;

            isQueryable = typeof(IQueryable).IsAssignableFrom(field.Resolve.Type);

            // update the Items field before we update the field.Resolve below
            itemsField = (Field)schema.GetActualField(field.ReturnType.SchemaType.Name, schema.SchemaFieldNamer("Items"), null);
            BuildTotalCountExpression(field, returnType, field.Resolve);
            itemsField.UpdateExpression(field.Resolve);

            // We steal any previous extensions as they were expected to work on the original Resolve which we moved to Edges
            extensions = field.Extensions.Take(field.Extensions.FindIndex(e => e is OffsetPagingExtension)).ToList();
            field.Extensions = field.Extensions.Skip(extensions.Count).ToList();
        }

        private Expression BuildTotalCountExpression(IField field, Type returnType, Expression resolve)
        {
            var totalCountExp = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Count", new Type[] { listType! }, resolve);
            var expression = Expression.MemberInit(
                Expression.New(returnType.GetConstructor(new[] { typeof(int), typeof(int?), typeof(int?) }), totalCountExp, Expression.PropertyOrField(field.ArgumentParam, "skip"), Expression.PropertyOrField(field.ArgumentParam, "take"))
            );
            return expression;
        }

        public override Expression GetExpression(Field field, Expression expression, ParameterExpression? argExpression, dynamic arguments, Expression context, ParameterReplacer parameterReplacer)
        {
            if (maxPageSize != null && arguments.Take > maxPageSize.Value)
                throw new ArgumentException($"Argument take can not be greater than {maxPageSize}.");

            // other extensions expect to run on the collection not our new shape
            Expression newItemsExp = expression;
            foreach (var extension in extensions!)
            {
                newItemsExp = extension.GetExpression(field, newItemsExp, argExpression, arguments, context, parameterReplacer);
            }
            // update the context
            newItemsExp = parameterReplacer.Replace(newItemsExp, field.FieldParam!, context);

            // Build our field expression and hold it for use in the next step
            var fieldExpression = BuildTotalCountExpression(field, returnType!, newItemsExp);
            fieldExpressionWithExtensionsApplied = newItemsExp;

            // Build our items expression with the paging
            newItemsExp = Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), "Take", new Type[] { listType! },
                Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), "Skip", new Type[] { listType! },
                    newItemsExp,
                    Expression.PropertyOrField(field.ArgumentParam, "skip")
                ),
                Expression.PropertyOrField(field.ArgumentParam, "take")
            );

            // we have current context update Items field
            itemsField!.UpdateExpression(newItemsExp);

            return fieldExpression;
        }
    }
}