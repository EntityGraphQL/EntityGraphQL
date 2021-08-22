using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public static class UseOffsetPagingExtension
    {
        /// <summary>
        /// Update field to implement paging with the Connection<> classes and metadata.
        /// Only call on a field that returns an IEnumerable
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static Field UseOffsetPaging(this Field field)
        {
            if (!field.Resolve.Type.IsEnumerableOrArray())
                throw new ArgumentException($"UseOffsetPaging must only be called on a field that returns an IEnumerable");
            field.AddExtension(new OffsetPagingExtension());
            return field;
        }
    }

    /// <summary>
    /// Sets up a few extensions to modify a simple collection expression - db.Movies.OrderBy() into a connection paging graph
    /// </summary>
    public class OffsetPagingExtension : IFieldExtension
    {
        private Expression originalEdgeExpression;
        private ParameterExpression tmpArgParam;
        private MethodCallExpression totalCountExp;
        private IField itemsField;
        private MethodCallExpression itemsFieldExp;

        public MethodCallExpression EdgeExpression { get; internal set; }

        /// <summary>
        /// Configure the field for a offset style paging field. Do as much as we can here as it is only executed once.
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="field"></param>
        public void Configure(ISchemaProvider schema, Field field)
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

            tmpArgParam = Expression.Parameter(field.ArgumentsType, "tmp_argParam");

            totalCountExp = Expression.Call(typeof(Queryable), "Count", new Type[] { listType }, field.Resolve);

            // update the Items field before we update the field.Resolve below
            itemsField = schema.GetActualField(field.ReturnType.SchemaType.Name, "items", null);
            itemsFieldExp = Expression.Call(typeof(QueryableExtensions), "Take", new Type[] { listType },
                Expression.Call(typeof(QueryableExtensions), "Skip", new Type[] { listType },
                    field.Resolve,
                        Expression.PropertyOrField(tmpArgParam, "skip")
                ),
                Expression.PropertyOrField(tmpArgParam, "take")
            );
            itemsField.UpdateExpression(itemsFieldExp);

            originalEdgeExpression = Expression.MemberInit(
                Expression.New(returnType.GetConstructor(new[] { typeof(int), typeof(int?), typeof(int?) }), totalCountExp, Expression.PropertyOrField(tmpArgParam, "skip"), Expression.PropertyOrField(tmpArgParam, "take"))
            );

            field.UpdateExpression(originalEdgeExpression);
        }

        public Expression GetExpression(Field field, ExpressionResult expression, ParameterExpression argExpression, dynamic arguments, Expression context, ParameterReplacer parameterReplacer)
        {
            // we have current context update Items field
            itemsField.UpdateExpression(
                parameterReplacer.Replace(
                    parameterReplacer.Replace(itemsFieldExp, field.FieldParam, context),
                    tmpArgParam,
                    argExpression
                )
            );

            return expression;
        }

        public (ExpressionResult baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam) ProcessExpressionPreSelection(GraphQLFieldType fieldType, ExpressionResult baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam, ParameterReplacer parameterReplacer)
        {
            return (baseExpression, selectionExpressions, selectContextParam);
        }

        public ExpressionResult ProcessFinalExpression(GraphQLFieldType fieldType, ExpressionResult expression, ParameterReplacer parameterReplacer)
        {
            return expression;
        }
    }
}