using EntityGraphQL.Schema.Directives;
using EntityGraphQL.Schema.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL.Schema
{
    public static class DeprecatedDirectiveExtensions
    {
        /// <summary>
        /// Marks this field as deprecated
        /// </summary>
        /// <param name="reason"></param>
        public static void Deprecate(this IField field, string? reason)
        {
            field.AddDirective(new DeprecatedDirective(reason));
        }
    }
}

namespace EntityGraphQL.Schema.Directives
{
    public class ObsoleteAttributeRegistration : IExtensionAttribute<ObsoleteAttribute>
    {
        public void ApplyExtension(IField field, ObsoleteAttribute attribute)
        {
            field.Deprecate(attribute.Message);
        }
    }

    public class DeprecatedDirective : ISchemaDirective
    {
        public DeprecatedDirective(string? reason = null)
        {
            Reason = reason;
        }

        public string? Reason { get; }

        public IEnumerable<TypeSystemDirectiveLocation> On => new[] {
            TypeSystemDirectiveLocation.FIELD_DEFINITION,
            TypeSystemDirectiveLocation.ARGUMENT_DEFINITION,
            TypeSystemDirectiveLocation.INPUT_FIELD_DEFINITION,
            TypeSystemDirectiveLocation.ENUM_VALUE
        };

        public void ProcessField(Models.Field field)
        {
            field.IsDeprecated = true;
            field.DeprecationReason = Reason;
        }

        public void ProcessEnumValue(EnumValue enumValue)
        {
            enumValue.IsDeprecated = true;
            enumValue.DeprecationReason = Reason;
        }

        public string ToGraphQLSchemaString()
        {
            return $"@deprecated(reason: \"{Reason}\")";
        }
    }
}