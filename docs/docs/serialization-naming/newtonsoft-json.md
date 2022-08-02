# Using NewtonSoft JSON

To avoid version conflicts with Newtonsoft.Json, EntityGraphQL has no reference to it and hence doesn't know what to do if it hits a `Jtoken` or `JObject`.

If you are not using EntityGraphQL.AspNet and are trying to deserialize incoming JSON into the `QueryRequest` and you have objects in the variables field Newtonsoft.Json will deserialize the incoming JSON objects into the variables dictionary as `JObject`/`JToken` types and EntityGraphQL doesn't know what to do with them when trying to mapping them to the field arguments.

_Note many JSON libraries will convert sub objects/arrays/etc into their own custom types when dealing with a `Dictionary<string, object>`. This can be used as an example to build other converters._

You can tell EntityGraphQL how to convert types when it is mapping incoming data classes/arguments using the `AddCustomTypeConverter(new MyICustomTypeConverter())` on the schema provider.

Here is an example to use this to handle Newtonsoft.Json types:

```cs
internal class JObjectTypeConverter : ICustomTypeConverter
{
    public Type Type => typeof(JObject);

    public object ChangeType(object value, Type toType, ISchemaProvider schema)
    {
        return ((JObject)value).ToObject(toType);
    }
}

internal class JTokenTypeConverter : ICustomTypeConverter
{
    public Type Type => typeof(JToken);

    public object ChangeType(object value, Type toType, ISchemaProvider schema)
    {
        return ((JToken)value).ToObject(toType);
    }
}

internal class JValueTypeConverter : ICustomTypeConverter
{
    public Type Type => typeof(JValue);

    public object ChangeType(object value, Type toType, ISchemaProvider schema)
    {
        return ((JValue)value).ToString();
    }
}

// Where you build schema

schema.AddCustomTypeConverter(new JObjectTypeConverter());
schema.AddCustomTypeConverter(new JTokenTypeConverter());
schema.AddCustomTypeConverter(new JValueTypeConverter());
```

Now EntityGraphQL can convert `JObject`, `JToken` & `JValue` types to classes/types using your version of Newtonsoft.Json. You can use `ICustomTypeConverter` to handle any customer conversion.
