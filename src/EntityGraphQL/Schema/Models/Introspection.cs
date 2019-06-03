//
// Built using quicktype
// https://app.quicktype.io/#l=cs&r=json2csharp
//
namespace EntityGraphQL.Schema.Models
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public partial class Introspection
    {
        [JsonProperty("errors")]
        public object[] Errors { get; set; }

        [JsonProperty("data")]
        public Data Data { get; set; }
    }

    public partial class Data
    {
        [JsonProperty("__schema")]
        public Schema Schema { get; set; }
    }

    public partial class Schema
    {
        [JsonProperty("queryType")]
        public QueryType QueryType { get; set; }

        [JsonProperty("mutationType")]
        public MutationType MutationType { get; set; }

        [JsonProperty("subscriptionType")]
        public SubscriptionType SubscriptionType { get; set; }

        [JsonProperty("types")]
        public TypeElement[] Types { get; set; }

        [JsonProperty("directives")]
        public Directives[] Directives { get; set; }
    }

    public partial class QueryType
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public partial class MutationType
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public partial class SubscriptionType
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public partial class TypeElement
    {
        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("fields")]
        public Field[] Fields { get; set; }

        [JsonProperty("inputFields")]
        public object InputFields { get; set; }

        [JsonProperty("interfaces")]
        public object[] Interfaces { get; set; }

        [JsonProperty("enumValues")]
        public object EnumValues { get; set; }

        [JsonProperty("possibleTypes")]
        public object PossibleTypes { get; set; }
    }

    public partial class Field
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("args")]
        public Arg[] Args { get; set; }

        [JsonProperty("type")]
        public Type Type { get; set; }

        [JsonProperty("isDeprecated")]
        public bool IsDeprecated { get; set; }

        [JsonProperty("deprecationReason")]
        public object DeprecationReason { get; set; }
    }

    public partial class Arg
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public object Description { get; set; }

        [JsonProperty("type")]
        public Type Type { get; set; }

        [JsonProperty("defaultValue")]
        public object DefaultValue { get; set; }
    }

    public partial class Type
    {
        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("ofType")]
        public Type OfType { get; set; }
    }

    public partial class Directives
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public object Description { get; set; }

        [JsonProperty("locations")]
        public string[] Locations { get; set; }

        [JsonProperty("args")]
        public Arg[] Args { get; set; }
    }





    public partial class Introspection
    {
        public static Introspection FromJson(string json) => JsonConvert.DeserializeObject<Introspection>(json, Converter.Settings);
    }

    public static class Serialize
    {
        public static string ToJson(this Introspection self) => JsonConvert.SerializeObject(self, Converter.Settings);
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }
}
