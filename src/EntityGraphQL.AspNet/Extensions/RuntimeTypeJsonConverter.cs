using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EntityGraphQL.AspNet.Extensions;

/// <summary>
/// Instructs the JsonSerializer to serialize an object as its runtime type and not the type parameter passed into the Write function.
/// https://stackoverflow.com/a/71074354/629083
/// </summary>
public class RuntimeTypeJsonConverter : JsonConverter<object>
{
    // this converter is only meant to work on reference types that are not strings
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsClass && typeToConvert != typeof(string);

    // default read implementation, the focus of this converter is the Write operation
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => JsonSerializer.Deserialize(ref reader, typeToConvert, options);

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        // Leave as much as we can to JsonSerializer.Serialize
        var isNotString = value is not string;
        if (value is IDictionary<string, object> dictionary)
        {
            WriteDictionary(writer, dictionary, ref options);
        }
        else if (isNotString && value is IEnumerable)
        {
            // if the value is an IEnumerable of any sorts, serialize it as a JSON array. Note that none of the properties of the IEnumerable are written, it is simply iterated over and serializes each object in the IEnumerable
            WriteIEnumerable(writer, value, options);
        }
        else if (isNotString && value.GetType().IsClass && value is not IEnumerable)
        {
            // if the value is a reference type and not null, serialize it as a JSON object.
            WriteObject(writer, value, ref options);
        }
        else
        {
            // otherwise just call the default serializer implementation of this Converter is asked to serialize anything not handled in the other cases
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }

    /// <summary>
    /// Writes the values for an dictionary into the Utf8JsonWriter
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert to Json.</param>
    /// <param name="options">An object that specifies the serialization options to use.</param>
    private static void WriteDictionary(Utf8JsonWriter writer, IDictionary<string, object> value, ref JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var key in value.Keys)
        {
            var propVal = value[key];
            var name = key;
            if (options.PropertyNamingPolicy != null)
            {
                name = options.PropertyNamingPolicy.ConvertName(name);
            }
            writer.WritePropertyName(name);
            if (propVal == null)
            {
                writer.WriteNullValue();
                continue;
            }
            JsonSerializer.Serialize(writer, propVal, propVal.GetType(), options);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes the values for an object into the Utf8JsonWriter
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert to Json.</param>
    /// <param name="options">An object that specifies the serialization options to use.</param>
    private static void WriteObject(Utf8JsonWriter writer, object value, ref JsonSerializerOptions options)
    {
        var type = value.GetType();

        writer.WriteStartObject();

        foreach (var member in type.GetProperties())
        {
            object? propVal = member.GetValue(value);
            var name = member.Name;
            if (options.PropertyNamingPolicy != null)
            {
                name = options.PropertyNamingPolicy.ConvertName(name);
            }
            writer.WritePropertyName(name);
            JsonSerializer.Serialize(writer, propVal, member.PropertyType, options);
        }

        if (options.IncludeFields)
        {
            foreach (var member in type.GetFields())
            {
                object? propVal = member.GetValue(value);
                var name = member.Name;
                if (options.PropertyNamingPolicy != null)
                {
                    name = options.PropertyNamingPolicy.ConvertName(name);
                }
                writer.WritePropertyName(name);
                JsonSerializer.Serialize(writer, propVal, member.FieldType, options);
            }
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes the values for an object that implements IEnumerable into the Utf8JsonWriter
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert to Json.</param>
    /// <param name="options">An object that specifies the serialization options to use.</param>
    private static void WriteIEnumerable(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (object? item in (value as IEnumerable)!)
        {
            if (item == null) // preserving null gaps in the IEnumerable
            {
                writer.WriteNullValue();
                continue;
            }

            JsonSerializer.Serialize(writer, item, item.GetType(), options);
        }

        writer.WriteEndArray();
    }
}
