using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public class SummarizeExtension : BaseFieldExtension
    {
        private Type? listType;
        private Type? methodType;
        //private Func<string, string>? fieldNamer;

        public SummarizeExtension()
        {
        }

        public override void Configure(ISchemaProvider schema, IField field)
        {
            if (field.ResolveExpression == null)
                throw new EntityGraphQLCompilerException($"SortExtension requires a Resolve function set on the field");

            if (!field.ResolveExpression.Type.IsEnumerableOrArray())
                throw new ArgumentException($"Expression for field {field.Name} must be a collection to use SortExtension. Found type {field.ReturnType.TypeDotnet}");

            listType = field.ReturnType.TypeDotnet.GetEnumerableOrArrayType()!;

            methodType = typeof(IQueryable).IsAssignableFrom(field.ReturnType.TypeDotnet) ?
                typeof(Queryable) : typeof(Enumerable);


            var summaryTypeName = $"{listType.Name}Summary";
            ISchemaType summarySchemaType;
            var type = typeof(Summary<>).MakeGenericType(listType);
            var isQueryable = typeof(IQueryable).IsAssignableFrom(field.ResolveExpression.Type);
            var queryableType = isQueryable ? typeof(Queryable) : typeof(Enumerable);

            if (!schema.HasType(summaryTypeName))
            {
                summarySchemaType = schema.AddType(type, summaryTypeName, $"Aggregate data for {listType}").AddAllFields();

                summarySchemaType.GetField("count", null).UpdateExpression(
                    Expression.Call(queryableType, "Count", new Type[] { listType! }, field.ResolveExpression)
                );

                var parameter = Expression.Parameter(listType);

                AddAggregregateField("Max", summarySchemaType, listType, field, queryableType, parameter);
                AddAggregregateField("Min", summarySchemaType, listType, field, queryableType, parameter);
                AddAggregregateField("Average", summarySchemaType, listType, field, queryableType, parameter);
                AddAggregregateField("Max", summarySchemaType, listType, field, queryableType, parameter);
                AddAggregregateField("Sum", summarySchemaType, listType, field, queryableType, parameter);
            }
            else
            {                
                summarySchemaType = schema.GetSchemaType(summaryTypeName, null);
            }

            //Create empty summary type
            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
                throw new EntityGraphQLCompilerException($"Could not create type {type.Name}");

            var expression = Expression.Lambda(Expression.MemberInit(Expression.New(constructor)), Expression.Parameter(listType));
            var gqlTypeInfo = new GqlTypeInfo(() => summarySchemaType, type);
            var schemaField = new Field(schema, "summarize", expression, "", null, gqlTypeInfo, null);
            var schemaType = schema.GetSchemaType(listType, null);
            schemaType.AddField(schemaField);
        }

        private void AddAggregregateField(string name, ISchemaType summarySchemaType, Type listType,  IField field, Type queryableType, ParameterExpression parameter)
        {
            var propTypes = new[] { typeof(int), typeof(float), typeof(decimal), typeof(long), typeof(double) };
            var props = listType.GetProperties().Where(x => propTypes.Contains(x.PropertyType));

            summarySchemaType.GetField(name.ToLower(), null).UpdateExpression(
                Expression.MemberInit(Expression.New(listType.GetConstructor(Type.EmptyTypes)),
                    props.Select(x =>
                        Expression.Bind(
                            listType.GetProperty(x.Name),
                            Expression.Convert(
                                Expression.Call(queryableType, name, new Type[] { listType! },
                                    new Expression[] { field.ResolveExpression!, Expression.Lambda(Expression.PropertyOrField(parameter, x.Name), parameter) }
                                ),
                                x.PropertyType
                            )
                        )
                    )
                )
            );
        }


        public override Expression? GetExpression(IField field, Expression expression, ParameterExpression? argExpression, dynamic? arguments, Expression context, IGraphQLNode? parentNode, bool servicesPass, ParameterReplacer parameterReplacer)
        {
            return expression;
        }
    }
}
