using System;
using System.Collections.Generic;

namespace EntityGraphQL.Schema.Models
{
    public partial class Schema
    {
        public Schema(TypeElement queryType, TypeElement? mutationType, TypeElement? subscriptionType, List<TypeElement> types, List<Directive> directives)
        {
            QueryType = queryType;
            MutationType = mutationType;
            SubscriptionType = subscriptionType;
            Types = types;
            Directives = directives;
        }

        public TypeElement QueryType { get; private set; }

        public TypeElement? MutationType { get; private set; }

        public TypeElement? SubscriptionType { get; private set; }

        public List<TypeElement> Types { get; private set; }

        public List<Directive> Directives { get; private set; }
    }

    public partial class TypeElement
    {
        public TypeElement() { }

        public TypeElement(string? kind, string? name)
        {
            Kind = kind;
            Name = name;
        }

        public string? Kind { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        // Fields is added dynamically so it is lazily loaded

        public InputValue[] InputFields { get; set; } = [];

        public TypeElement[] Interfaces { get; set; } = [];

        public EnumValue[] EnumValues { get; set; } = [];

        public TypeElement[] PossibleTypes { get; set; } = [];
        public TypeElement? OfType { get; set; }
        public bool OneField { get; set; }

        // may be non-null for custom SCALAR, otherwise null.
        public string? SpecifiedByURL { get; set; }
    }

    public partial class Field
    {
        public Field(string name, TypeElement type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; private set; }

        public string? Description { get; set; }

        public IEnumerable<InputValue> Args { get; set; } = Array.Empty<InputValue>();

        public TypeElement Type { get; private set; }

        public bool IsDeprecated { get; set; }

        public string? DeprecationReason { get; set; }
    }

    public class InputValue
    {
        public InputValue(string name, TypeElement type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; private set; }
        public string? Description { get; set; }
        public TypeElement Type { get; private set; }
        public string? DefaultValue { get; set; }
    }

    public partial class Directive
    {
        public Directive(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }

        public string? Description { get; set; }

        public IEnumerable<string> Locations { get; set; } = Array.Empty<string>();

        public IEnumerable<InputValue> Args { get; set; } = Array.Empty<InputValue>();
    }

    public partial class EnumValue
    {
        public EnumValue(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }

        public string? Description { get; set; }

        public bool IsDeprecated { get; set; }

        public string? DeprecationReason { get; set; }
    }
}
