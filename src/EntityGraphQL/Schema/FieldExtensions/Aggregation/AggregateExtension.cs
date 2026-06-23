using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions;

/// <summary>
/// Adds aggregate data (count + min/max/sum/average over numeric fields) for a collection field.
///
/// The aggregate is computed over the whole collection (the same expression the field resolves to).
/// We generate a non-list object type (e.g. PersonAggregate) whose fields resolve aggregate calls over
/// the collection, and expose it according to the configured <see cref="AggregatePlacement"/>.
/// </summary>
public class AggregateExtension : BaseFieldExtension
{
    private readonly AggregatePlacement placement;
    private readonly LambdaExpression? fieldSelection;

    // numeric types we expose aggregates for (Min/Max work for all of these; Sum/Average need an overload - see WidenForSumAverage)
    private static readonly HashSet<Type> NumericTypes = [typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal)];

    // numeric types that lack a Sum/Average overload on Queryable/Enumerable, mapped to one that has it
    private static readonly Dictionary<Type, Type> WidenForSumAverage = new() { { typeof(short), typeof(int) } };

    public AggregateExtension(AggregatePlacement placement, LambdaExpression? fieldSelection = null)
    {
        this.placement = placement;
        this.fieldSelection = fieldSelection;
    }

    /// <summary>
    /// The element field names (schema-named) to expose aggregates for, or null to expose all aggregatable fields.
    /// Built from the optional fieldSelection expression (e.g. x => new { x.Height, x.Id } or x => x.Height).
    /// </summary>
    private HashSet<string>? GetAllowedFieldNames(ISchemaProvider schema)
    {
        if (fieldSelection == null)
            return null;

        var body = fieldSelection.Body;
        if (body.NodeType == ExpressionType.Convert)
            body = ((UnaryExpression)body).Operand;

        var names = body switch
        {
            NewExpression newExp when newExp.Members != null => newExp.Members.Select(m => m.Name),
            MemberExpression member => [member.Member.Name],
            _ => throw new EntityGraphQLSchemaException($"{nameof(AggregateExtension)} field selection must be a member (x => x.Field) or an anonymous object of members (x => new {{ x.A, x.B }})"),
        };
        return new HashSet<string>(names.Select(schema.SchemaFieldNamer), StringComparer.OrdinalIgnoreCase);
    }

    public override void Configure(ISchemaProvider schema, IField field)
    {
        if (field.FieldType != GraphQLQueryFieldType.Query)
            throw new EntityGraphQLSchemaException($"{nameof(AggregateExtension)} cannot be used on a {field.FieldType} field ({field.Name}).");

        if (field.ResolveExpression == null)
            throw new EntityGraphQLSchemaException($"{nameof(AggregateExtension)} requires a Resolve function set on the field");

        // when chained after UseOffsetPaging/UseConnectionPaging the field returns a paging wrapper, not a collection
        var paging = field.Extensions.OfType<OffsetPagingExtension>().Cast<IFieldExtension>().Concat(field.Extensions.OfType<ConnectionPagingExtension>()).FirstOrDefault();
        var isPaged = paging != null;

        // Auto uses the paging wrapper when paged, otherwise the OwnWrapper { items, aggregate } shape (which
        // handles every field uniformly - services, custom arguments). SiblingField is opt-in only: it is
        // additive (doesn't change the field's shape) so it can be added to an existing field, but it is a
        // separate field and so cannot share a service-backed resolver (it would invoke the service twice).
        var effectivePlacement = placement == AggregatePlacement.Auto ? (isPaged ? AggregatePlacement.PagingWrapper : AggregatePlacement.OwnWrapper) : placement;

        if (field.Services.Count > 0 && effectivePlacement == AggregatePlacement.SiblingField)
            throw new EntityGraphQLSchemaException(
                $"{nameof(AggregatePlacement)}.{nameof(AggregatePlacement.SiblingField)} cannot be used on a service-backed collection field ({field.Name}) as it would invoke the service twice. Use {nameof(AggregatePlacement)}.{nameof(AggregatePlacement.OwnWrapper)} or {nameof(AggregatePlacement.PagingWrapper)}."
            );

        if (effectivePlacement == AggregatePlacement.PagingWrapper)
        {
            if (!isPaged)
                throw new EntityGraphQLSchemaException(
                    $"{nameof(AggregatePlacement)}.{nameof(AggregatePlacement.PagingWrapper)} requires {nameof(UseAggregateExtension.UseAggregate)} to be chained after UseOffsetPaging()/UseConnectionPaging() on field {field.Name}."
                );

            ConfigurePagingWrapper(schema, field, paging!, GetAllowedFieldNames(schema));
            return;
        }

        if (!field.ResolveExpression.Type.IsEnumerableOrArray())
            throw new EntityGraphQLSchemaException($"Expression for field {field.Name} must be a collection to use {nameof(AggregateExtension)}. Found type {field.ReturnType.TypeDotnet}");

        if (field.FieldParam == null)
            throw new EntityGraphQLSchemaException($"{nameof(AggregateExtension)} requires a field parameter on field {field.Name}");

        var elementType =
            field.ReturnType.TypeDotnet.GetEnumerableOrArrayType()
            ?? throw new EntityGraphQLSchemaException($"Could not determine element type for field {field.Name} to use {nameof(AggregateExtension)}");

        var isQueryable = field.ResolveExpression.Type.IsGenericTypeQueryable();
        var elementSchemaType = field.ReturnType.SchemaType;
        var aggregateType = GetOrCreateAggregateType(schema, elementSchemaType, elementType, isQueryable, GetAllowedFieldNames(schema));

        if (effectivePlacement == AggregatePlacement.OwnWrapper)
            ConfigureOwnWrapper(schema, field, elementSchemaType, elementType, aggregateType, isQueryable);
        else
            AddSiblingField(schema, field, aggregateType);
    }

    /// <summary>
    /// Replace the field's return type with a "{Element}WithAggregate" wrapper { items, aggregate }. Both children
    /// resolve the collection identity, so any filter/sort extension already on the field applies to both.
    /// </summary>
    private static void ConfigureOwnWrapper(ISchemaProvider schema, IField field, ISchemaType elementSchemaType, Type elementType, ISchemaType aggregateType, bool isQueryable)
    {
        var wrapperName = $"{elementSchemaType.Name}WithAggregate";
        ISchemaType wrapperType;
        if (schema.HasType(wrapperName))
        {
            wrapperType = schema.Type(wrapperName);
        }
        else
        {
            var markerType = typeof(AggregateWithItems<>).MakeGenericType(elementType);
            wrapperType = schema.AddType(markerType, wrapperName, $"A collection of {elementSchemaType.Name} with aggregate data");
            var collType = (isQueryable ? typeof(IQueryable<>) : typeof(IEnumerable<>)).MakeGenericType(elementType);

            var itemsParam = Expression.Parameter(collType, "c");
            var itemsReturn = new GqlTypeInfo(() => elementSchemaType, collType) { IsList = true };
            wrapperType.AddField(
                new Field(
                    schema,
                    wrapperType,
                    schema.SchemaFieldNamer("Items"),
                    Expression.Lambda(itemsParam, itemsParam),
                    "The items in the collection",
                    (Dictionary<string, ArgType>?)null,
                    itemsReturn,
                    null
                )
            );

            var aggParam = Expression.Parameter(collType, "c");
            var aggReturn = new GqlTypeInfo(() => aggregateType, aggregateType.TypeDotnet) { IsList = false };
            wrapperType.AddField(
                new Field(
                    schema,
                    wrapperType,
                    schema.SchemaFieldNamer("Aggregate"),
                    Expression.Lambda(aggParam, aggParam),
                    "Aggregate data over the collection",
                    (Dictionary<string, ArgType>?)null,
                    aggReturn,
                    null
                )
            );
        }

        field.Returns(new GqlTypeInfo(() => wrapperType, wrapperType.TypeDotnet) { IsList = false });
    }

    private static void ConfigurePagingWrapper(ISchemaProvider schema, IField field, IFieldExtension paging, HashSet<string>? allowedFieldNames)
    {
        var original = paging is OffsetPagingExtension off ? off.OriginalFieldExpression : ((ConnectionPagingExtension)paging).OriginalFieldExpression;
        if (original == null)
            throw new EntityGraphQLSchemaException($"Could not resolve the source collection for aggregate on field {field.Name}");

        var elementType =
            original.Type.GetEnumerableOrArrayType() ?? throw new EntityGraphQLSchemaException($"Could not determine element type for field {field.Name} to use {nameof(AggregateExtension)}");
        var isQueryable = original.Type.IsGenericTypeQueryable();
        var elementSchemaType = schema.GetSchemaType(elementType, null);

        var aggregateType = GetOrCreateAggregateType(schema, elementSchemaType, elementType, isQueryable, allowedFieldNames);

        // add an "aggregate" field onto the paging wrapper type (OffsetPage/Connection)
        var wrapperType = field.ReturnType.SchemaType;
        var aggFieldName = schema.SchemaFieldNamer("Aggregate");
        if (wrapperType.HasField(aggFieldName, null))
            return;

        var collType = (isQueryable ? typeof(IQueryable<>) : typeof(IEnumerable<>)).MakeGenericType(elementType);
        var identityParam = Expression.Parameter(collType, "c");
        var returnType = new GqlTypeInfo(() => aggregateType, aggregateType.TypeDotnet) { IsList = false };

        var aggField = new Field(
            schema,
            wrapperType,
            aggFieldName,
            Expression.Lambda(identityParam, identityParam),
            $"Aggregate data over the full {field.Name} collection",
            (Dictionary<string, ArgType>?)null,
            returnType,
            null
        );
        aggField.AddExtension(new AggregatePagingExtension());
        wrapperType.AddField(aggField);
    }

    private static void AddSiblingField(ISchemaProvider schema, IField field, ISchemaType aggregateType)
    {
        var parent = field.FromType;
        var siblingName = $"{field.Name}Aggregate";
        if (parent.HasField(siblingName, null))
            return;

        // resolve to the same collection expression as the source field
        var resolve = Expression.Lambda(field.ResolveExpression!, field.FieldParam!);
        var returnType = new GqlTypeInfo(() => aggregateType, aggregateType.TypeDotnet) { IsList = false };

        var siblingField = new Field(schema, parent, siblingName, resolve, $"Aggregate data over {field.Name}", (Dictionary<string, ArgType>?)null, returnType, field.RequiredAuthorization);

        // Carry the source field's arguments so the aggregate is parameterised/filtered the same way and can be
        // queried as e.g. peopleAggregate(filter: "...", minId: 5). We reuse the source field's argument
        // parameter and type so the resolve body's argument references stay valid, then copy the argument
        // definitions (except sort - it doesn't affect an aggregate and we don't re-apply the sort extension).
        var sortArgName = schema.SchemaFieldNamer("Sort");
        if (field.ArgumentsParameter != null)
        {
            siblingField.ArgumentsParameter = field.ArgumentsParameter;
            siblingField.ExpressionArgumentType = field.ExpressionArgumentType;
            foreach (var arg in field.Arguments)
            {
                if (!string.Equals(arg.Key, sortArgName, StringComparison.OrdinalIgnoreCase))
                    siblingField.Arguments.Add(arg.Key, arg.Value);
            }
        }

        parent.AddField(siblingField);

        // Re-apply the filter extension(s) that ran before aggregate so the aggregate respects the same filter.
        // We attach the already-configured instance directly (not via AddExtension) so it does not re-add its
        // argument - that is carried above - while still applying its Where at execution time. The instance has
        // no per-request state and its element type matches, so sharing it with the sibling is safe.
        foreach (var ext in field.Extensions.TakeWhile(e => e is not AggregateExtension).Where(e => e is FilterExpressionExtension))
            siblingField.Extensions.Add(ext);
    }

    /// <summary>
    /// The aggregate functions exposed, function-first (Hasura style). Each becomes a nested object type
    /// (e.g. PersonMinAggregate) exposing the element fields it can apply to.
    /// </summary>
    private static readonly AggregateFunction[] Functions = [new("Min", isMinMax: true), new("Max", isMinMax: true), new("Sum", isMinMax: false), new("Average", isMinMax: false)];

    /// <summary>
    /// Build (or reuse) the aggregate object type for an element type. The type exposes (function-first):
    ///   count: Int
    ///   min/max: {Element}{Func}Aggregate { ...comparable fields }
    ///   sum/average: {Element}{Func}Aggregate { ...numeric fields }
    /// Each leaf field resolves an aggregate call over the collection (which is the field's context).
    /// </summary>
    private static ISchemaType GetOrCreateAggregateType(ISchemaProvider schema, ISchemaType elementSchemaType, Type elementType, bool isQueryable, HashSet<string>? allowedFieldNames)
    {
        var aggTypeName = $"{elementSchemaType.Name}Aggregate";
        if (schema.HasType(aggTypeName))
            return schema.Type(aggTypeName);

        var markerType = typeof(Aggregation<>).MakeGenericType(elementType);
        var aggType = schema.AddType(markerType, aggTypeName, $"Aggregate data over a collection of {elementSchemaType.Name}");
        var methodClass = isQueryable ? typeof(System.Linq.Queryable) : typeof(System.Linq.Enumerable);
        var collType = (isQueryable ? typeof(IQueryable<>) : typeof(IEnumerable<>)).MakeGenericType(elementType);

        // count
        var countParam = Expression.Parameter(collType, "c");
        var countCall = Expression.Call(methodClass, nameof(System.Linq.Enumerable.Count), [elementType], countParam);
        aggType.AddField(
            new Field(
                schema,
                aggType,
                schema.SchemaFieldNamer("Count"),
                Expression.Lambda(countCall, countParam),
                "Total number of items",
                (Dictionary<string, ArgType>?)null,
                SchemaBuilder.MakeGraphQlType(schema, false, typeof(int), null, "count", aggType),
                null
            )
        );

        // the scalar element fields we can aggregate (optionally restricted to a caller-supplied selection)
        var aggregatableFields = elementSchemaType
            .GetFields()
            .Where(f =>
                !f.Name.StartsWith("__", StringComparison.Ordinal)
                && !f.ReturnType.IsList
                && !f.Arguments.Any()
                && f.Services.Count == 0
                && f.ResolveExpression != null
                && f.FieldParam != null
                && (allowedFieldNames == null || allowedFieldNames.Contains(f.Name))
            )
            .ToList();

        // one nested object per aggregate function, exposing the fields that function applies to
        foreach (var func in Functions)
        {
            var fieldsForFunc = aggregatableFields.Where(f => func.AppliesTo(f.ReturnType.TypeDotnet)).ToList();
            if (fieldsForFunc.Count == 0)
                continue;

            var funcType = BuildFunctionType(schema, elementSchemaType, elementType, func, fieldsForFunc, collType, methodClass);

            // the function field resolves the collection identity (passed down to its leaf children)
            var identityParam = Expression.Parameter(collType, "c");
            var funcReturnType = new GqlTypeInfo(() => funcType, funcType.TypeDotnet) { IsList = false };
            aggType.AddField(
                new Field(
                    schema,
                    aggType,
                    schema.SchemaFieldNamer(func.Name),
                    Expression.Lambda(identityParam, identityParam),
                    $"{func.Name} of each field",
                    (Dictionary<string, ArgType>?)null,
                    funcReturnType,
                    null
                )
            );
        }

        return aggType;
    }

    private static ISchemaType BuildFunctionType(ISchemaProvider schema, ISchemaType elementSchemaType, Type elementType, AggregateFunction func, List<IField> fields, Type collType, Type methodClass)
    {
        var typeName = $"{elementSchemaType.Name}{func.Name}Aggregate";
        if (schema.HasType(typeName))
            return schema.Type(typeName);

        // unique marker type per (element, function) - the sentinel field makes the cached dynamic type distinct
        var markerType = LinqRuntimeTypeBuilder.GetDynamicType(new Dictionary<string, Type> { { $"__{elementType.Name}_{func.Name}", typeof(int) } }, $"{elementSchemaType.Name}_{func.Name}_Agg");
        var funcType = schema.AddType(markerType, typeName, $"{func.Name} of each field over a collection of {elementSchemaType.Name}");

        foreach (var elementField in fields)
        {
            var collParam = Expression.Parameter(collType, "c");
            var selectorParam = Expression.Parameter(elementType, "x");
            var selectorBody = new ParameterReplacer().Replace(elementField.ResolveExpression!, elementField.FieldParam!, selectorParam);

            // Sum/Average have no Queryable/Enumerable overload for some numeric types (e.g. short), so widen the
            // selector to a type that does (short -> int). Min/Max are generic on TResult and need no widening.
            if (!func.IsMinMax)
            {
                var nonNull = Nullable.GetUnderlyingType(selectorBody.Type) ?? selectorBody.Type;
                if (WidenForSumAverage.TryGetValue(nonNull, out var widen))
                {
                    var target = Nullable.GetUnderlyingType(selectorBody.Type) != null ? typeof(Nullable<>).MakeGenericType(widen) : widen;
                    selectorBody = Expression.Convert(selectorBody, target);
                }
            }

            // Min/Max/Average over an empty set throws for a non-nullable value type ("Sequence contains no
            // elements"). Project the selector to a nullable value type so these return null on an empty set
            // instead (the aggregate of nothing is null). Sum of an empty set is 0, so it is left as-is.
            if (func.Name != nameof(Enumerable.Sum) && selectorBody.Type.IsValueType && Nullable.GetUnderlyingType(selectorBody.Type) == null)
                selectorBody = Expression.Convert(selectorBody, typeof(Nullable<>).MakeGenericType(selectorBody.Type));

            var selector = Expression.Lambda(selectorBody, selectorParam);

            // Min/Max are generic on <TSource, TResult>; Sum/Average resolve overload by selector return type
            Expression aggCall = func.IsMinMax
                ? Expression.Call(methodClass, func.Name, [elementType, selectorBody.Type], collParam, selector)
                : Expression.Call(methodClass, func.Name, [elementType], collParam, selector);

            funcType.AddField(
                new Field(
                    schema,
                    funcType,
                    elementField.Name,
                    Expression.Lambda(aggCall, collParam),
                    $"{func.Name} of {elementField.Name}",
                    (Dictionary<string, ArgType>?)null,
                    SchemaBuilder.MakeGraphQlType(schema, false, aggCall.Type, null, elementField.Name, funcType),
                    null
                )
            );
        }

        return funcType;
    }

    private sealed class AggregateFunction(string name, bool isMinMax)
    {
        public string Name { get; } = name;

        /// <summary>Min/Max (generic on result type, apply to any comparable field) vs Sum/Average (numeric only).</summary>
        public bool IsMinMax { get; } = isMinMax;

        public bool AppliesTo(Type fieldType)
        {
            var nonNull = Nullable.GetUnderlyingType(fieldType) ?? fieldType;
            if (NumericTypes.Contains(nonNull))
                return true;
            // Min/Max also work for dates and strings
            return IsMinMax && (nonNull == typeof(DateTime) || nonNull == typeof(DateTimeOffset) || nonNull == typeof(string));
        }
    }
}
