---
title: "Custom Extensions"
metaTitle: "Creating your own field extensions - EntityGraphQL"
metaDescription: "Creating your own field extensions"
---

Field extensions let you move common patterns or use cases into an extension method to easily apply the logic across many fields. To implement an extension you implement the `IFieldExtension` interface and create an extension method.

Here is an extension method to add a format argument to a field.

```
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
}
```

`FormatStringExtension` needs to implement `IFieldExtension`.

```
public class FormatStringExtension : IFieldExtension
{
    // Configure the field. Do as much as we can here as it is only called once on registered.
    public void Configure(ISchemaProvider schema, Field field)
    {

    }

    // This is called on compilation of a query if the query references this field
    // Good opportunity to check arguments
    public Expression GetExpression(Field field, ExpressionResult expression, ParameterExpression argExpression, dynamic arguments, Expression context, ParameterReplacer parameterReplacer)
    {
        if (arguments.last == null && arguments.first == null)
            throw new ArgumentException($"Please provide at least the first or last argument");
    }

    // Called at expression execution time.
    public (ExpressionResult baseExpression, Dictionary<string, CompiledField> selectionExpressions) ProcessFinalExpression(GraphQLFieldType fieldType, ExpressionResult baseExpression, Dictionary<string, CompiledField> selectionExpressions)
    {
    }
}
```

