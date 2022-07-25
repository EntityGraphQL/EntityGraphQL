using System.Collections.Generic;

namespace EntityGraphQL.Schema.Models
{
    public partial class Schema
    {
        public Schema(TypeElement queryType, TypeElement mutationType, SubscriptionType? subscriptionType, List<TypeElement> types, List<Directive> directives)
        {
            QueryType = queryType;
            MutationType = mutationType;
            SubscriptionType = subscriptionType;
            Types = types;
            Directives = directives;
        }

        public TypeElement QueryType { get; private set; }

        public TypeElement MutationType { get; private set; }

        public SubscriptionType? SubscriptionType { get; private set; }

        public List<TypeElement> Types { get; private set; }

        public List<Directive> Directives { get; private set; }
    }

    public partial class SubscriptionType
    {
        public SubscriptionType(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }
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

        public InputValue[] InputFields { get; set; } = new InputValue[0];

        public TypeElement[] Interfaces { get; set; } = new TypeElement[0];

        public EnumValue[] EnumValues { get; set; } = new EnumValue[0];

        public TypeElement[] PossibleTypes { get; set; } = new TypeElement[0];
        public TypeElement? OfType { get; set; }
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

        public IEnumerable<InputValue> Args { get; set; } = new InputValue[0];

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

        public IEnumerable<string> Locations { get; set; } = new string[0];

        public IEnumerable<InputValue> Args { get; set; } = new InputValue[0];
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
