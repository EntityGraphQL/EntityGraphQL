﻿//
// Built using quicktype
// https://app.quicktype.io/#l=cs&r=json2csharp
//
using System.Collections.Generic;

namespace EntityGraphQL.Schema.Models
{
    public partial class Schema
    {
        public TypeElement QueryType { get; set; }

        public TypeElement MutationType { get; set; }

        public SubscriptionType SubscriptionType { get; set; }

        public List<TypeElement> Types { get; set; }

        public List<Directive> Directives { get; set; }
    }

    public partial class SubscriptionType
    {
        public string Name { get; set; }
    }

    public partial class TypeElement
    {
        public TypeElement()
        {
            EnumValues = new EnumValue[] { };
            Interfaces = new TypeElement[] { };
            PossibleTypes = new TypeElement[] { };
            InputFields = new InputValue[] { };
        }

        public string Kind { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        // Fields is added dynamically so it is lazily loaded

        public InputValue[] InputFields { get; set; }

        public TypeElement[] Interfaces { get; set; }

        public EnumValue[] EnumValues { get; set; }

        public TypeElement[] PossibleTypes { get; set; }
        public TypeElement OfType { get; set; }
    }

    public partial class Field
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public InputValue[] Args { get; set; }

        public TypeElement Type { get; set; }

        public bool IsDeprecated { get; set; }

        public string DeprecationReason { get; set; }
    }

    public class InputValue
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public TypeElement Type { get; set; }
        public string DefaultValue { get; set; }
    }

    public partial class Directive
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public string[] Locations { get; set; }

        public InputValue[] Args { get; set; }
    }

    public partial class EnumValue
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public bool IsDeprecated { get; set; }

        public string DeprecationReason { get; set; }
    }
}
