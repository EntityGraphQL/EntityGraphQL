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
    public class RuntimeTypeJsonConverter<T> : JsonConverter<T> where T : class
    {
        private static readonly Dictionary<Type, List<MemberInfo>> _knownProps = new Dictionary<Type, List<MemberInfo>>(); //cache mapping a Type to its array of public properties to serialize
        private static readonly Dictionary<Type, JsonConverter> _knownConverters = new Dictionary<Type, JsonConverter>(); //cache mapping a Type to its respective RuntimeTypeJsonConverter instance that was created to serialize that type. 
        private static readonly Dictionary<Type, Type> _knownGenerics = new Dictionary<Type, Type>(); //cache mapping a Type to the type of RuntimeTypeJsonConverter generic type definition that was created to serialize that type

        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsClass && typeToConvert != typeof(string); //this converter is only meant to work on reference types that are not strings
        }

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var deserialized = JsonSerializer.Deserialize(ref reader, typeToConvert, options); //default read implementation, the focus of this converter is the Write operation
            return (T?)deserialized;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (value is IDictionary<string, object> dictionary)
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
                JsonSerializer.Serialize(writer, value, options);
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
                var propType = propVal.GetType(); //get the runtime type of the value regardless of what the property info says the PropertyType should be

                if (propType.IsClass && propType != typeof(string)) //if the property type is a valid type for this JsonConverter to handle, do some reflection work to get a RuntimeTypeJsonConverter appropriate for the sub-object
                {
                    Type generic = GetGenericConverterType(propType); //get a RuntimeTypeJsonConverter<T> Type appropriate for the sub-object
                    JsonConverter converter = GetJsonConverter(generic); //get a RuntimeTypeJsonConverter<T> instance appropriate for the sub-object

                    //look in the options list to see if we don't already have one of these converters in the list of converters in use (we may already have a converter of the same type, but it may not be the same instance as our converter variable above)
                    var found = false;
                    foreach (var converterInUse in options.Converters)
                    {
                        if (converterInUse.GetType() == generic)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (found == false) //not in use, make a new options object clone and add the new converter to its Converters list (which is immutable once passed into the Serialize method).
                    {
                        options = new JsonSerializerOptions(options);
                        options.Converters.Add(converter);
                    }

                    //Write(writer, propVal,  options);
                    JsonSerializer.Serialize(writer, propVal, options);
                }
                else //not one of our sub-objects, serialize it like normal
                {
                    JsonSerializer.Serialize(writer, propVal, options);
                }
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes the values for an object into the Utf8JsonWriter
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The value to convert to Json.</param>
        /// <param name="options">An object that specifies the serialization options to use.</param>
        private void WriteObject(Utf8JsonWriter writer, T value, ref JsonSerializerOptions options)
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
                var propType = propVal.GetType(); //get the runtime type of the value regardless of what the property info says the PropertyType should be

                if (propType.IsClass && propType != typeof(string)) //if the property type is a valid type for this JsonConverter to handle, do some reflection work to get a RuntimeTypeJsonConverter appropriate for the sub-object
                {
                    Type generic = GetGenericConverterType(propType); //get a RuntimeTypeJsonConverter<T> Type appropriate for the sub-object
                    JsonConverter converter = GetJsonConverter(generic); //get a RuntimeTypeJsonConverter<T> instance appropriate for the sub-object

                    //look in the options list to see if we don't already have one of these converters in the list of converters in use (we may already have a converter of the same type, but it may not be the same instance as our converter variable above)
                    var found = false;
                    foreach (var converterInUse in options.Converters)
                    {
                        if (converterInUse.GetType() == generic)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (found == false) //not in use, make a new options object clone and add the new converter to its Converters list (which is immutable once passed into the Serialize method).
                    {
                        options = new JsonSerializerOptions(options);
                        options.Converters.Add(converter);
                    }

                    JsonSerializer.Serialize(writer, propVal, propType, options);
                }
                else //not one of our sub-objects, serialize it like normal
                {
                    JsonSerializer.Serialize(writer, propVal, options);
                }
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Gets or makes RuntimeTypeJsonConverter generic type to wrap the given type parameter.
        /// </summary>
        /// <param name="propType">The type to get a RuntimeTypeJsonConverter generic type for.</param>
        /// <returns></returns>
        private Type GetGenericConverterType(Type propType)
        {
            Type? generic = null;
            if (_knownGenerics.ContainsKey(propType) == false)
            {
                generic = typeof(RuntimeTypeJsonConverter<>).MakeGenericType(propType);
                _knownGenerics.Add(propType, generic);
            }
            else
            {
                generic = _knownGenerics[propType];
            }

            return generic;
        }

        /// <summary>
        /// Gets or creates the corresponding RuntimeTypeJsonConverter that matches the given generic type defintion.
        /// </summary>
        /// <param name="genericType">The generic type definition of a RuntimeTypeJsonConverter.</param>
        /// <returns></returns>
        private JsonConverter GetJsonConverter(Type genericType)
        {
            JsonConverter? converter = null;
            if (_knownConverters.ContainsKey(genericType) == false)
            {
                converter = (JsonConverter)Activator.CreateInstance(genericType)!;
                _knownConverters.Add(genericType, converter);
            }
            else
            {
                converter = _knownConverters[genericType];
            }

            return converter;
        }



        /// <summary>
        /// Gets all the public properties of a Type.
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        private IEnumerable<MemberInfo> GetPropertyAndFieldInfos(Type t, JsonSerializerOptions options)
        {            
            if (!_knownProps.ContainsKey(t))
            {
                var props = new List<MemberInfo>();
                props.AddRange(t.GetProperties());

                if (options.IncludeFields)
                {
                    props.AddRange(t.GetFields());
                }
                _knownProps.Add(t, props);
                return props;
            }
            else
            {
                return _knownProps[t];
            }            
        }

        /// <summary>
        /// Writes the values for an object that implements IEnumerable into the Utf8JsonWriter
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The value to convert to Json.</param>
        /// <param name="options">An object that specifies the serialization options to use.</param>
        private void WriteIEnumerable(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
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
