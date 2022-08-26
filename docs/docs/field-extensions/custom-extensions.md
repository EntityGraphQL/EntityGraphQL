---
sidebar_position: 4
---

# Custom Extensions

Field extensions let you move common patterns or use cases into an extension method to easily apply the logic across many fields. To implement an extension you implement the `IFieldExtension` interface and create an extension method. You can have your extension applied via an `Attribute` in `SchemaBuilder` by having your attribute extend `FieldExtensionAttribute`.

Here is an extension method to add a format argument to a field.

```cs
public static class UseFormatExtension
{
    public static Field UseFormat(this Field field)
    {
        if (field.Resolve.Type != typeof(string))
            throw new ArgumentException($"UseFormat must only be called on a field that returns a string");

        // register extension on field
        field.AddExtension(new FormatStringExtension());
        return field;
    }

    public class UseFormatAttribute : FieldExtensionAttribute
    {
        public override void ApplyExtension(Field field)
        {
            field.UseFormat(DefaultPageSize, MaxPageSize);
        }
    }
}
```

`FormatStringExtension` needs to implement `IFieldExtension` or extend `BaseFieldExtension`.

```cs
public class FormatStringExtension : IFieldExtension
{
    // Configure the field. Do as much as we can here as it is only called once on registered.
    public void Configure(ISchemaProvider schema, IField field)
    {

    }

    // This is called on compilation of a query if the query references this field
    // Good opportunity to check arguments
    // Most often you can update the expression here and return your new one
    public Expression GetExpression(Field field, Expression expression, ParameterExpression? argExpression, dynamic? arguments, Expression context, IGraphQLNode? parentNode, bool servicesPass, ParameterReplacer parameterReplacer)
    {
        if (arguments.last == null && arguments.first == null)
            throw new ArgumentException($"Please provide at least the first or last argument");

        // build the expression that calls your format logic/method
        var call = Expression.Cal(...);
        return call;
    }

    public Expression ProcessScalarExpression(Expression expression, ParameterReplacer parameterReplacer)
    {
        // Only called for scalar fields (result in data, not a selection)
        // called at the final stage just before being used in the final expression for execution
        // Often no need to do anything here
        return expression;
    }

    public (Expression baseExpression, Dictionary<IFieldKey, CompiledField> selectionExpressions, ParameterExpression selectContextParam) ProcessExpressionSelection(GraphQLFieldType fieldType, Expression baseExpression, Dictionary<IFieldKey, CompiledField> selectionExpressions, ParameterExpression? selectContextParam, bool servicesPass, ParameterReplacer parameterReplacer)
    {
        // Called for object projection and collection fields. Giving you an opportunity to modify
        // the selection expression or the selection base expression.
        // ConnectionEdgeNodeExtension provides a good example of this from UseConnectionPaging
        return (baseExpression, selectionExpressions, selectContextParam);
    }
}
```

See [`FilterExpressionExtension`](https://github.com/EntityGraphQL/EntityGraphQL/blob/master/src/EntityGraphQL/Schema/FieldExtensions/Filter/FilterExpressionExtension.cs) for a simple example. [`SortExtension`](https://github.com/EntityGraphQL/EntityGraphQL/blob/master/src/EntityGraphQL/Schema/FieldExtensions/Sorting/SortExtension.cs) for a sightly more complex one. Or [`ConnectionPagingExtension.cs`](https://github.com/EntityGraphQL/EntityGraphQL/blob/master/src/EntityGraphQL/Schema/FieldExtensions/ConnectionPaging/ConnectionPagingExtension.cs) for an example that changes the shape of the field (from a collection to a Connection object).
