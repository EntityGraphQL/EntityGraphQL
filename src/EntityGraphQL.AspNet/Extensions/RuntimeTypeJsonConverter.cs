using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EntityGraphQL.AspNet.Extensions
{
    /// <summary>
    /// Instructs the JsonSerializer to serialize an object as its runtime type and not the type parameter passed into the Write function.
    /// https://stackoverflow.com/a/71074354/629083
    /// </summary>
    public class RuntimeTypeJsonConverter : JsonConverter<object> 
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsClass && typeToConvert != typeof(string); //this converter is only meant to work on reference types that are not strings
        }

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var deserialized = JsonSerializer.Deserialize(ref reader, typeToConvert, options); //default read implementation, the focus of this converter is the Write operation
            return deserialized;
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value is string)
            {
                JsonSerializer.Serialize(writer, value);
            }
            else if (value is IDictionary<string, object> dictionary)
            {
                WriteDictionary(writer, dictionary, ref options);
            }
            else if (value is IEnumerable) //if the value is an IEnumerable of any sorts, serialize it as a JSON array. Note that none of the properties of the IEnumerable are written, it is simply iterated over and serializes each object in the IEnumerable
            {
                WriteIEnumerable(writer, value, options);
            }
            else if (value != null && value.GetType().IsClass == true) //if the value is a reference type and not null, serialize it as a JSON object.
            {                
                WriteObject(writer, value, ref options);
            }
            else //otherwise just call the default serializer implementation of this Converter is asked to serialize anything not handled in the other two cases
            {
                JsonSerializer.Serialize(writer, value, value!.GetType(), options);
            }
        }

        /// <summary>
        /// Writes the values for an dictionary into the Utf8JsonWriter
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The value to convert to Json.</param>
        /// <param name="options">An object that specifies the serialization options to use.</param>
        private void WriteDictionary(Utf8JsonWriter writer, IDictionary<string, object> value, ref JsonSerializerOptions options)
        {
            var type = value.GetType();

            writer.WriteStartObject();

            foreach (var key in value.Keys)
            {
                var propVal = value[key];
                if (propVal == null) continue; //don't include null values in the final graph

                writer.WritePropertyName(key);
                Write(writer, propVal, options);
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes the values for an object into the Utf8JsonWriter
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The value to convert to Json.</param>
        /// <param name="options">An object that specifies the serialization options to use.</param>
        private void WriteObject(Utf8JsonWriter writer, object value, ref JsonSerializerOptions options)
        {
            var type = value.GetType();

            //get all the public properties that we will be writing out into the object
            var members = GetPropertyAndFieldInfos(type, options);

            writer.WriteStartObject();

            foreach (var member in members)
            {
                object? propVal = null;

                if(member is PropertyInfo p)
                {
                    propVal = p.GetValue(value);
                }

                if (member is FieldInfo f)
                {
                    propVal = f.GetValue(value);
                }

                if (propVal == null) continue; //don't include null values in the final graph

                writer.WritePropertyName(member.Name);
                Write(writer, propVal, options);               
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Gets all the public properties of a Type.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private IEnumerable<MemberInfo> GetPropertyAndFieldInfos(Type t, JsonSerializerOptions options)
        {            
            var props = new List<MemberInfo>();
            props.AddRange(t.GetProperties());

            if (options.IncludeFields)
            {
                props.AddRange(t.GetFields());
            }
            return props;
        }

        /// <summary>
        /// Writes the values for an object that implements IEnumerable into the Utf8JsonWriter
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The value to convert to Json.</param>
        /// <param name="options">An object that specifies the serialization options to use.</param>
        private void WriteIEnumerable(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();

            foreach (object? item in (value as IEnumerable)!)
            {
                if (item == null) //preserving null gaps in the IEnumerable
                {
                    writer.WriteNullValue();
                    continue;
                }

                JsonSerializer.Serialize(writer, item, item.GetType(), options);
            }

            writer.WriteEndArray();
        }
    }
}
