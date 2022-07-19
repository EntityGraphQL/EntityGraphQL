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

            if (!schema.HasType(summaryTypeName))
            {
                var isQueryable = typeof(IQueryable).IsAssignableFrom(field.ResolveExpression.Type);
                var queryableType = isQueryable ? typeof(Queryable) : typeof(Enumerable);

                summarySchemaType = schema.AddType(type, summaryTypeName, $"Aggregate data for {listType}").AddAllFields();
                
                summarySchemaType.GetField("count", null).UpdateExpression(
                    Expression.Call(queryableType, "Count", new Type[] { listType! }, field.ResolveExpression)
                );

                //summarySchemaType.GetField("max", null).UpdateExpression(
                //    Expression.Lambda(
                //        Expression.MemberInit(Expression.New(listType.GetConstructor(Type.EmptyTypes))),
                //        Expression.Parameter(listType)
                //    )
                //    //Expression.Call(queryableType, "Count", new Type[] { listType! }, field.ResolveExpression)
                //);
            }
            else
            {
                summarySchemaType = schema.GetSchemaType(summaryTypeName, null);
            }

            var gqlTypeInfo = new GqlTypeInfo(() => summarySchemaType, type);


            var contextParam = Expression.Parameter(listType);
            var expression = Expression.Lambda(Expression.MemberInit(Expression.New(type.GetConstructor(Type.EmptyTypes))), contextParam);
            
            //todo argsType: new SummarizeInput()
            var schemaField = new Field(schema, "summarize", expression, "", null, gqlTypeInfo, null);

            var schemaType = schema.GetSchemaType(listType, null);
            schemaType.AddField(schemaField);
        }

        public override Expression? GetExpression(IField field, Expression expression, ParameterExpression? argExpression, dynamic? arguments, Expression context, IGraphQLNode? parentNode, bool servicesPass, ParameterReplacer parameterReplacer)
        {
            return expression;
        }
    }
}
