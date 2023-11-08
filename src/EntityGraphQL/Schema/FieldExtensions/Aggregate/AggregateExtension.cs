using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions;

/// <summary>
/// Builds a field to query aggregate data on a list field.
/// 
/// E.g. Given a field that returns a list of people: people: [Person!]! it will add a new field peopleAggregate: PeopleAggregate 
/// similar to the following:
/// 
/// schema.AddType<PeopleAggregate>("PeopleAggregate", "Aggregate people", type =>
/// {
///     type.AddField("count", (c) => c.Count(), "Count of people");
///     type.AddField("heightMin", (c) => c.Min(p => p.Height), "Min height");
///     type.AddField("heightMax", (c) => c.Max(p => p.Height), "Max height");
///     type.AddField("heightAvg", (c) => c.Average(p => p.Height), "Average height");
///     type.AddField("heightSum", (c) => c.Sum(p => p.Height), "Sum of height");
/// });
/// schema.Query().AddField("peopleAggregate", ctx => ctx.People, "Aggregate people")
///     .Returns("PeopleAggregate");
///     
/// Where PeopleAggregate is a new dotnet type used to differentiate the return type from a normal list of people.
/// 
/// public class PeopleAggregate : IEnumerable<Person>
/// {
///     public IEnumerator<Person> GetEnumerator() => throw new System.NotImplementedException();
///     IEnumerator IEnumerable.GetEnumerator() => throw new System.NotImplementedException();
/// }
///
/// If this is at the root level of the query it would build a LINQ expression query similar to the following:
/// (MyContext ctx) => new
/// {
///     count = ctx.People.Count(),
///     minHeight = ctx.People.Min(p => p.Height),
///     maxHeight = ctx.People.Max(p => p.Height),
///     avgHeight = ctx.People.Average(p => p.Height),
///     sumHeight = ctx.People.Sum(p => p.Height)
/// };
/// </summary>
public class AggregateExtension : BaseFieldExtension
{
    private static readonly object addAggregateTypeLock = new();
    private readonly string? fieldName;
    private readonly List<string>? aggregateFieldList;
    private readonly bool fieldListIsExclude;
    private static int dupeCnt = 1;

    public AggregateExtension(string? fieldName, IEnumerable<string>? aggregateFieldList, bool excludeFields)
    {
        this.fieldName = fieldName;
        this.aggregateFieldList = aggregateFieldList?.ToList();
        fieldListIsExclude = excludeFields;
    }

    public override void Configure(ISchemaProvider schema, IField field)
    {
        if (field.ResolveExpression == null)
            throw new EntityGraphQLCompilerException($"ConnectionPagingExtension requires a Resolve function set on the field");

        if (!field.ResolveExpression.Type.IsEnumerableOrArray())
            throw new ArgumentException($"Expression for field {field.Name} must be a collection to use ConnectionPagingExtension. Found type {field.ReturnType.TypeDotnet}");

        var schemaTypeName = $"{field.FromType.Name}{field.Name.FirstCharToUpper()}Aggregate";
        var fieldName = this.fieldName ?? $"{field.Name}Aggregate";
        var listElementType = field.ReturnType.TypeDotnet.GetEnumerableOrArrayType()!;

        // likely not to happen in normal use but the unit tests will hit this as it tries to create the same type multiple times
        lock (addAggregateTypeLock)
        {
            // first build type and fetch it from the dynamic types - this will reuse the type if it already exists
            var aggregateDotnetType = GetDotnetType(field, listElementType);

            // Then check if we have that in the schema already
            var aggregateSchemaType = schema.HasType(aggregateDotnetType) ? schema.GetSchemaType(aggregateDotnetType, null) : null;
            if (aggregateSchemaType == null)
            {
                // The type may be different but it may have the same name, see test TestDifferentOptionsOnSameTypeDifferentFields
                if (schema.HasType(schemaTypeName))
                    schemaTypeName = $"{schemaTypeName}{dupeCnt++}"; // could do this better

                // use reflection to call AddAggregateTypeToSchema
                var addTypeMethod = typeof(AggregateExtension).GetMethod(nameof(AddAggregateTypeToSchema), BindingFlags.NonPublic | BindingFlags.Static);
                var genericAddTypeMethod = addTypeMethod!.MakeGenericMethod(aggregateDotnetType, listElementType);
                aggregateSchemaType = (genericAddTypeMethod.Invoke(this, new object[] { schema, schemaTypeName, field }) as ISchemaType)!;

                var contextParam = Expression.Parameter(aggregateSchemaType.TypeDotnet, schemaTypeName);
                // set up all the fields on the aggregate type
                ForEachPossibleAggregateField(field.ReturnType.SchemaType.GetFields(),
                    (possibleAggregateField, returnFieldType) =>
                    {
                        AddAggregateFieldByReflection("Average", aggregateDotnetType, aggregateSchemaType, possibleAggregateField, contextParam, listElementType);
                        AddAggregateFieldByReflection("Sum", aggregateDotnetType, aggregateSchemaType, possibleAggregateField, contextParam, listElementType);
                        AddAggregateFieldByReflection("Min", aggregateDotnetType, aggregateSchemaType, possibleAggregateField, contextParam, listElementType);
                        AddAggregateFieldByReflection("Max", aggregateDotnetType, aggregateSchemaType, possibleAggregateField, contextParam, listElementType);
                    },
                    (possibleAggregateField, returnFieldType) =>
                    {
                        AddAggregateFieldByReflection("Min", aggregateDotnetType, aggregateSchemaType, possibleAggregateField, contextParam, listElementType, true);
                        AddAggregateFieldByReflection("Max", aggregateDotnetType, aggregateSchemaType, possibleAggregateField, contextParam, listElementType, true);
                    }
                );
            }
            else
            {
                schemaTypeName = aggregateSchemaType.Name;
            }
        }

        var newField = new Field(schema, field.FromType, fieldName, Expression.Lambda(field.ResolveExpression, field.FieldParam!), $"Aggregate data for {field.Name}", null, new GqlTypeInfo(() => schema.Type(schemaTypeName), schema.Type(schemaTypeName).TypeDotnet), null);
        field.FromType.AddField(newField);
    }

    private Type GetDotnetType(IField field, Type listElementType)
    {
        var fields = new Dictionary<string, Type> {
            { "count", typeof(int) }
        };
        ForEachPossibleAggregateField(field.ReturnType.SchemaType.GetFields(),
            (possibleAggregateField, returnFieldType) =>
            {
                fields.Add($"{possibleAggregateField.Name}Average", returnFieldType);
                fields.Add($"{possibleAggregateField.Name}Sum", returnFieldType);
                fields.Add($"{possibleAggregateField.Name}Min", returnFieldType);
                fields.Add($"{possibleAggregateField.Name}Max", returnFieldType);
            },
            (possibleAggregateField, returnFieldType) =>
            {
                fields.Add($"{possibleAggregateField.Name}Min", returnFieldType);
                fields.Add($"{possibleAggregateField.Name}Max", returnFieldType);
            }
        );
        var aggregateDotnetType = LinqRuntimeTypeBuilder.GetDynamicType(fields, field.Name, null,
            new[] { typeof(IEnumerable<>).MakeGenericType(listElementType) },
            aggregateDotnetTypeDef =>
            {
                // define the IEnumerable<T> implementation
                var getEnumeratorMethod = typeof(IEnumerable<>).MakeGenericType(listElementType).GetMethod("GetEnumerator");
                var getEnumeratorIL = aggregateDotnetTypeDef.DefineMethod(getEnumeratorMethod!.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig, getEnumeratorMethod.ReturnType, Type.EmptyTypes).GetILGenerator();
                getEnumeratorIL.Emit(OpCodes.Newobj, typeof(NotImplementedException).GetConstructor(Type.EmptyTypes)!);
                var getEnumeratorMethod2 = typeof(System.Collections.IEnumerable).GetMethod("GetEnumerator");
                var getEnumeratorIL2 = aggregateDotnetTypeDef.DefineMethod(getEnumeratorMethod2!.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig, getEnumeratorMethod2.ReturnType, Type.EmptyTypes).GetILGenerator();
                getEnumeratorIL2.Emit(OpCodes.Newobj, typeof(NotImplementedException).GetConstructor(Type.EmptyTypes)!);
            })!;
        return aggregateDotnetType;
    }

    private void ForEachPossibleAggregateField(IEnumerable<IField> fields, Action<IField, Type> aggregateFieldActionNumeric, Action<IField, Type> aggregateFieldTyped)
    {
        foreach (IField possibleAggregateField in fields)
        {
            if (possibleAggregateField.Name.StartsWith("__", StringComparison.InvariantCulture)
                || possibleAggregateField.ResolveExpression == null)
                continue;
            if (aggregateFieldList != null)
            {
                if (fieldListIsExclude && aggregateFieldList.Contains(possibleAggregateField.Name))
                    continue;
                if (!fieldListIsExclude && !aggregateFieldList.Contains(possibleAggregateField.Name))
                    continue;
            }

            // use reflection to build aggregateSchemaType.AddField("min", (c) => c.Min(fieldExp), "Min value"); ETC
            // average & sum can only be done on numeric types from Queryable.Average/Sum
            var returnFieldType = possibleAggregateField.ReturnType.TypeDotnet;
            if (returnFieldType == typeof(int) || returnFieldType == typeof(int?)
                || returnFieldType == typeof(long) || returnFieldType == typeof(long?)
                || returnFieldType == typeof(double) || returnFieldType == typeof(double?)
                || returnFieldType == typeof(decimal) || returnFieldType == typeof(decimal?)
                || returnFieldType == typeof(float) || returnFieldType == typeof(float?))
            {
                aggregateFieldActionNumeric(possibleAggregateField, returnFieldType);
            }
            else if (returnFieldType == typeof(DateTimeOffset) || returnFieldType == typeof(DateTimeOffset?)
                    || returnFieldType == typeof(DateTime) || returnFieldType == typeof(DateTime?)
                    )
            {
                aggregateFieldTyped(possibleAggregateField, returnFieldType);
            }
        }
    }

    private static void AddAggregateFieldByReflection(string method, Type aggregateDotnetType, ISchemaType aggregateSchemaType, IField field, ParameterExpression contextParam, Type listElementType, bool typedReturn = false)
    {
        var fieldName = $"{field.Name}{method}";
        var fieldDescription = $"{method} of {field.Name}";
        var genTypes = typedReturn ? new[] { listElementType, field.ReturnType.TypeDotnet } : new[] { listElementType };
        var call = Expression.Call(typeof(Enumerable), method, genTypes, contextParam, Expression.Lambda(field.ResolveExpression!, field.FieldParam!));
        var fieldExp = Expression.Lambda(call, contextParam);
        //  find public Field AddField<TReturn>(string name, Expression<Func<TBaseType, TReturn>> fieldSelection, string? description)
        var addFieldMethod = aggregateSchemaType.GetType().GetMethods()
                                .SingleOrDefault(m => m.Name == nameof(ISchemaType.AddField)
                                                && m.IsGenericMethod
                                                && m.ReturnType == typeof(Field)
                                                && m.GetGenericArguments().Length == 1
                                                && m.GetParameters().Length == 3);
        var genericAddFieldMethod = addFieldMethod!.MakeGenericMethod(fieldExp.ReturnType);
        genericAddFieldMethod.Invoke(aggregateSchemaType, new object[] { fieldName, fieldExp, fieldDescription });
    }

    private static ISchemaType AddAggregateTypeToSchema<TType, TListElement>(ISchemaProvider schema, string schemaTypeName, IField field) where TType : class, IEnumerable<TListElement>
    {
        var aggregateType = schema.AddType<TType>(schemaTypeName, $"Aggregate {field.Name}", type =>
        {
            type.AddField("count", (c) => c.Count(), "Count of items");
        });
        return aggregateType;
    }
}