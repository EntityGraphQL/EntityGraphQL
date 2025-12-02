# Using NewtonSoft JSON

To avoid version conflicts with Newtonsoft.Json, EntityGraphQL has no reference to it and hence doesn't know what to do if it hits a `JToken` or `JObject`.

If you are not using EntityGraphQL.AspNet and are trying to deserialize incoming JSON into the `QueryRequest` and you have objects in the variables field Newtonsoft.Json will deserialize the incoming JSON objects into the variables dictionary as `JObject`/`JToken` types and EntityGraphQL doesn't know what to do with them when trying to mapping them to the field arguments.

_Note many JSON libraries will convert sub objects/arrays/etc into their own custom types when dealing with a `Dictionary<string, object>`. This can be used as an example to build other converters._

You can tell EntityGraphQL how to convert types when it is mapping incoming data classes/arguments using the `AddCustomTypeConverter` method on the schema provider.

Here is an example to use this to handle Newtonsoft.Json types:

```cs
// Where you build schema

// Convert JObject to any target type
schema.AddCustomTypeConverter<JObject>(
    (jObj, toType, schema) => jObj.ToObject(toType)!
);

// Convert JToken to any target type
schema.AddCustomTypeConverter<JToken>(
    (jToken, toType, schema) => jToken.ToObject(toType)!
);

// Convert JValue to any target type (returns string)
schema.AddCustomTypeConverter<JValue>(
    (jValue, toType, schema) => jValue.ToString()
);
```

Now EntityGraphQL can convert `JObject`, `JToken` & `JValue` types to classes/types using your version of Newtonsoft.Json.
