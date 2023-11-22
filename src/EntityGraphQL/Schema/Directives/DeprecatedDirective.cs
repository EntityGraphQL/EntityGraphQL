using EntityGraphQL.Schema.Directives;
using EntityGraphQL.Schema.Models;
using System;
using System.Collections.Generic;

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
    public class ObsoleteAttributeHandler : AbstractExtensionAttributeHandler<ObsoleteAttribute>
    {
        public override void ApplyExtension(IField field, ObsoleteAttribute attribute)
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

        public IEnumerable<TypeSystemDirectiveLocation> Location => new[] {
            TypeSystemDirectiveLocation.FieldDefinition,
            TypeSystemDirectiveLocation.ArgumentDefinition,
            TypeSystemDirectiveLocation.InputFieldDefinition,
            TypeSystemDirectiveLocation.EnumValue
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