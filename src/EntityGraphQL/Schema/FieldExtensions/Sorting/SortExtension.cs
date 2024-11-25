using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions;

public class SortExtension : BaseFieldExtension
{
    private ISchemaType? schemaReturnType;
    private Type? listType;
    private Type? methodType;
    private Func<string, string>? fieldNamer;
    private readonly Type? fieldSelectionType;
    private readonly List<ISort> defaultSorts;
    private readonly ParameterExpression? fieldSelectionParam;
    private readonly Dictionary<string, Expression>? fieldSelectionExpressions;
    private readonly bool useSchemaFields;

    public SortExtension(LambdaExpression? fieldSelection, bool useSchemaFields, params ISort[] defaultSorts)
    {
        this.fieldSelectionType = fieldSelection?.ReturnType;
        this.defaultSorts = defaultSorts?.ToList() ?? [];
        this.fieldSelectionParam = fieldSelection?.Parameters.First();
        if (fieldSelection?.Body is NewExpression newExp)
            this.fieldSelectionExpressions = newExp.Members?.Select((m, i) => new { m.Name, Expression = newExp.Arguments[i] }).ToDictionary(x => x.Name, x => x.Expression);
        this.useSchemaFields = useSchemaFields;
    }

    public override void Configure(ISchemaProvider schema, IField field)
    {
        if (field.ResolveExpression == null)
            throw new EntityGraphQLCompilerException($"SortExtension requires a Resolve function set on the field");

        if (!field.ResolveExpression.Type.IsEnumerableOrArray())
            throw new ArgumentException($"Expression for field {field.Name} must be a collection to use SortExtension. Found type {field.ReturnType.TypeDotnet}");

        if (!schema.HasType(typeof(SortDirection)))
            schema.AddEnum<SortDirection>("SortDirectionEnum", "Sort direction enum");
        schemaReturnType = field.ReturnType.SchemaType;
        listType = field.ReturnType.TypeDotnet.GetEnumerableOrArrayType()!;
        methodType = typeof(IQueryable).IsAssignableFrom(field.ReturnType.TypeDotnet) ? typeof(Queryable) : typeof(Enumerable);

        fieldNamer = schema.SchemaFieldNamer;
        var sortInputName = $"{field.FromType.Name}{field.Name.FirstCharToUpper()}SortInput".FirstCharToUpper();
        ISchemaType schemaSortType;
        var argSortType = MakeSortType(field);
        // look type reuse type. Type is not recreated if it uses the same fields
        if (schema.HasType(argSortType))
            schemaSortType = schema.GetSchemaType(argSortType, false, null);
        else
        {
            schemaSortType = schema.AddInputType(argSortType, sortInputName, $"Sort arguments for {field.Name}").AddAllFields();
        }

        var argType = typeof(SortInput<>).MakeGenericType(schemaSortType.TypeDotnet);
        var argInstance = Activator.CreateInstance(argType)!;
        if (defaultSorts.Count > 0)
        {
            var defaultSortValues = Activator.CreateInstance(typeof(List<>).MakeGenericType(schemaSortType.TypeDotnet))!;

            foreach (var defaultSort in defaultSorts)
            {
                var defaultSortValue = Activator.CreateInstance(schemaSortType.TypeDotnet)!;
                // if the field is not there the default sort is not exposed in the API we the schema does not need to know about the default
                var fieldExp = defaultSort.SortExpression.Body;
                if (fieldExp.NodeType == ExpressionType.Convert)
                    fieldExp = ((UnaryExpression)fieldExp).Operand;
                var sortValueField = schemaSortType.TypeDotnet.GetField(((MemberExpression)fieldExp).Member.Name);
                if (sortValueField != null)
                {
                    sortValueField.SetValue(defaultSortValue, defaultSort.Direction);
                    ((IList)defaultSortValues).Add(defaultSortValue);
                    argType.GetProperty("Sort")!.SetValue(argInstance, defaultSortValues);
                }
            }
        }
        field.AddArguments(argInstance);
    }

    private Type MakeSortType(IField field)
    {
        // Build the field args
        Dictionary<string, Type> fields = [];
        var directionType = typeof(SortDirection?);

        if (useSchemaFields)
        {
            foreach (var schemaField in schemaReturnType!.GetFields())
            {
                if (schemaField.Name.StartsWith("__", StringComparison.CurrentCulture))
                    continue;
                if (IsNotInputType(schemaField.ReturnType.TypeDotnet))
                    continue;
                fields.Add(schemaField.Name, directionType);
            }
        }
        else
        {
            var typeWithSortFields = fieldSelectionType ?? listType!;
            foreach (var prop in typeWithSortFields.GetProperties())
            {
                if (IsNotInputType(prop.PropertyType) || GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(prop))
                    continue;
                fields.Add(prop.Name, directionType);
            }
            foreach (var prop in typeWithSortFields.GetFields())
            {
                if (IsNotInputType(prop.FieldType) || GraphQLIgnoreAttribute.ShouldIgnoreMemberFromInput(prop))
                    continue;
                fields.Add(prop.Name, directionType);
            }
        }
        // build SortInput - need a unique name if they use sort on another field with the same name
        var argSortType = LinqRuntimeTypeBuilder.GetDynamicType(fields, field.Name);
        return argSortType;
    }

    private static bool IsNotInputType(Type type)
    {
        return type.IsEnumerableOrArray() || (type.IsClass && type != typeof(string));
    }

    public override Expression? GetExpression(
        IField field,
        Expression expression,
        ParameterExpression? argumentParam,
        dynamic? arguments,
        Expression context,
        IGraphQLNode? parentNode,
        bool servicesPass,
        ParameterReplacer parameterReplacer
    )
    {
        // things are sorted already and the field shape has changed
        if (servicesPass)
            return expression;

        // default sort gets put in arguments
        if (arguments != null && arguments!.Sort != null && arguments!.Sort.Count > 0)
        {
            var sortMethod = "OrderBy";
            foreach (var sort in arguments!.Sort)
            {
                // find the field that tells us the order field
                foreach (var fieldInfo in ((Type)sort.GetType()).GetFields())
                {
                    var direction = (SortDirection?)fieldInfo.GetValue(sort);
                    if (!direction.HasValue)
                        continue;

                    string method = sortMethod;

                    if (direction.Value == SortDirection.DESC)
                        method += "Descending";

                    var listParam = Expression.Parameter(listType!);

                    Type sortReturnType;
                    if (fieldSelectionExpressions != null && fieldSelectionExpressions.TryGetValue(fieldInfo.Name, out var sortExpression))
                    {
                        sortReturnType = sortExpression.Type;
                        sortExpression = parameterReplacer.Replace(sortExpression, fieldSelectionParam!, listParam);
                    }
                    else
                    {
                        var schemaField = schemaReturnType!.GetField(fieldNamer!(fieldInfo.Name), null);
                        sortReturnType = schemaField.ReturnType.TypeDotnet;
                        sortExpression = schemaField.ResolveExpression ?? Expression.PropertyOrField(listParam, fieldInfo.Name);
                        listParam = schemaField.FieldParam!;
                    }

                    expression = Expression.Call(methodType!, method, [listType!, sortReturnType], expression, Expression.Lambda(sortExpression, listParam));
                    break;
                }
                sortMethod = "ThenBy";
            }
        }
        else if (defaultSorts.Count > 0)
        {
            var thenBy = false;
            foreach (var defaultSort in defaultSorts)
            {
                var listParam = Expression.Parameter(listType!);
                expression = Expression.Call(
                    methodType!,
                    defaultSort.Direction == SortDirection.ASC ? (thenBy ? "ThenBy" : "OrderBy") : (thenBy ? "ThenByDescending" : "OrderByDescending"),
                    new Type[] { listType!, defaultSort.SortExpression.Body.Type },
                    expression,
                    parameterReplacer.Replace(defaultSort.SortExpression, defaultSort.SortExpression.Parameters.First(), listParam)
                );
                thenBy = true;
            }
        }
        return expression;
    }
}
