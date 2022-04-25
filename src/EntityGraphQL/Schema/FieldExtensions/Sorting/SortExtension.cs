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

        public override void Configure(ISchemaProvider schema, IField field)
        {
            if (field.ResolveExpression == null)
                throw new EntityGraphQLCompilerException($"SortExtension requires a Resolve function set on the field");

            if (!field.ResolveExpression.Type.IsEnumerableOrArray())
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
                // build SortInput - need a unique name if they use sort on another field with the same name
                var argSortType = LinqRuntimeTypeBuilder.GetDynamicType(fields, $"{sortInputName}-{Guid.NewGuid()}");
                schemaSortType = schema.AddInputType(argSortType, sortInputName, $"Sort arguments for {field.Name}").AddAllFields();
            }

            var argType = typeof(SortInput<>).MakeGenericType(schemaSortType.TypeDotnet);
            field.AddArguments(Activator.CreateInstance(argType)!);
        }

        private bool IsNotInputType(Type type)
        {
            return type.IsEnumerableOrArray() || (type.IsClass && type != typeof(string));
        }

        public override Expression GetExpression(Field field, Expression expression, ParameterExpression? argExpression, dynamic? arguments, Expression context, IGraphQLNode? parentNode, bool servicesPass, ParameterReplacer parameterReplacer)
        {
            // things are sorted already and the field shape has changed
            if (servicesPass)
                return expression;

            if (arguments != null && arguments!.Sort != null && arguments!.Sort.Count > 0)
            {
                var sortMethod = "OrderBy";
                foreach (var sort in arguments!.Sort)
                {
                    // find the field that tells us the order field
                    foreach (var fieldInfo in ((Type)sort.GetType()).GetFields())
                    {
                        var direction = (SortDirectionEnum?)fieldInfo.GetValue(sort);
                        if (!direction.HasValue)
                            continue;

                        string method = sortMethod;

                        if (direction.Value == SortDirectionEnum.DESC)
                            method += "Descending";

                        var schemaField = schemaReturnType!.GetField(fieldNamer!(fieldInfo.Name), null);

                        var listParam = Expression.Parameter(listType!);
                        Expression sortField = listParam;
                        expression = Expression.Call(
                            methodType!,
                            method,
                            new Type[] { listType!, schemaField.ReturnType.TypeDotnet },
                            expression,
                            Expression.Lambda(Expression.PropertyOrField(sortField, fieldInfo.Name), listParam)
                        );
                        break;
                    }
                    sortMethod = "ThenBy";
                }
            }
            else if (defaultSort != null)
            {
                var listParam = Expression.Parameter(listType!);
                expression = Expression.Call(
                        methodType!,
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