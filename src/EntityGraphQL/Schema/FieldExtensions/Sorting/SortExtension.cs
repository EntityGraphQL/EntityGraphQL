using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions
{
    public class SortExtension : BaseFieldExtension
    {
        private ISchemaType? schemaReturnType;
        private Type? listType;
        private Type? methodType;
        private Func<string, string>? fieldNamer;
        private readonly Type? fieldSelectionType;
        private readonly LambdaExpression? defaultSort;
        private readonly SortDirectionEnum? direction;

        public SortExtension(Type? fieldSelectionType, LambdaExpression? defaultSort, SortDirectionEnum? direction)
        {
            this.fieldSelectionType = fieldSelectionType;
            this.defaultSort = defaultSort;
            this.direction = direction;
        }

        public override void Configure(ISchemaProvider schema, Field field)
        {
            if (field.Resolve == null)
                throw new EntityGraphQLCompilerException($"SortExtension requires a Resolve function set on the field");

            if (!field.Resolve.Type.IsEnumerableOrArray())
                throw new ArgumentException($"Expression for field {field.Name} must be a collection to use SortExtension. Found type {field.ReturnType.TypeDotnet}");

            if (!schema.HasType(typeof(SortDirectionEnum)))
                schema.AddEnum("SortDirectionEnum", typeof(SortDirectionEnum), "Sort direction enum");
            schemaReturnType = field.ReturnType.SchemaType;
            listType = field.ReturnType.TypeDotnet.GetEnumerableOrArrayType()!;
            methodType = typeof(IQueryable).IsAssignableFrom(field.ReturnType.TypeDotnet) ?
                typeof(Queryable) : typeof(Enumerable);

            fieldNamer = schema.SchemaFieldNamer;
            var sortInputName = $"{field.Name}SortInput".FirstCharToUpper();
            ISchemaType schemaSortType;
            if (schema.HasType(sortInputName))
                schemaSortType = schema.Type(sortInputName);
            else
            {
                var typeWithSortFields = fieldSelectionType ?? listType;
                // Build the field args
                Dictionary<string, Type> fields = new();
                var directionType = typeof(SortDirectionEnum?);
                foreach (var prop in typeWithSortFields.GetProperties())
                {
                    if (IsNotInputType(prop.PropertyType))
                        continue;
                    fields.Add(prop.Name, directionType);
                }
                foreach (var prop in typeWithSortFields.GetFields())
                {
                    if (IsNotInputType(prop.FieldType))
                        continue;
                    fields.Add(prop.Name, directionType);
                }
                // build SortInput
                var argSortType = LinqRuntimeTypeBuilder.GetDynamicType(fields);
                schemaSortType = schema.AddInputType(argSortType, sortInputName, $"Sort arguments for {field.Name}").AddAllFields();
            }

            var argType = typeof(SortInput<>).MakeGenericType(schemaSortType.TypeDotnet);
            field.AddArguments(Activator.CreateInstance(argType));
        }

        private bool IsNotInputType(Type type)
        {
            return type.IsEnumerableOrArray() || (type.IsClass && type != typeof(string));
        }

        public override Expression GetExpression(Field field, Expression expression, ParameterExpression? argExpression, dynamic? arguments, Expression context, bool servicesPass, ParameterReplacer parameterReplacer)
        {
            // things are sorted already and the field shape has changed
            if (servicesPass)
                return expression;

            if (arguments != null && arguments!.Sort != null)
            {
                var first = true;
                foreach (var fieldInfo in ((Type)arguments!.Sort.GetType()).GetFields())
                {
                    var direction = (SortDirectionEnum?)fieldInfo.GetValue(arguments.Sort);
                    if (!direction.HasValue)
                        continue;

                    string method;
                    if (first)
                    {
                        method = "OrderBy";
                        first = false;
                    }
                    else
                        method = "ThenBy";
                    if (direction.Value == SortDirectionEnum.DESC)
                        method += "Descending";

                    var schemaField = schemaReturnType!.GetField(fieldNamer!(fieldInfo.Name), null);

                    var listParam = Expression.Parameter(listType);
                    Expression sortField = listParam;
                    expression = Expression.Call(
                        methodType,
                        method,
                        new Type[] { listType!, schemaField.ReturnType.TypeDotnet },
                        expression,
                        Expression.Lambda(Expression.PropertyOrField(sortField, fieldInfo.Name), listParam)
                    );
                }
            }
            else if (defaultSort != null)
            {
                var listParam = Expression.Parameter(listType);
                expression = Expression.Call(
                        methodType,
                        direction == SortDirectionEnum.ASC ? "OrderBy" : "OrderByDescending",
                        new Type[] { listType!, defaultSort.Body.Type },
                        expression,
                        parameterReplacer.Replace(defaultSort, defaultSort.Parameters.First(), listParam)
                    );
            }
            return expression;
        }
    }
}