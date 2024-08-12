using System.Collections.Generic;
using EntityGraphQL.Schema.Directives;

namespace EntityGraphQL.Schema
{
    public static class SchemaDirectiveExtensions
    {
        public static void ProcessField(this IEnumerable<ISchemaDirective> directives, Models.Field field)
        {
            foreach (var directive in directives)
            {
                directive.ProcessField(field);
            }
        }

        public static void ProcessType(this IEnumerable<ISchemaDirective> directives, Models.TypeElement type)
        {
            foreach (var directive in directives)
            {
                directive.ProcessType(type);
            }
        }

        public static void ProcessEnumValue(this IEnumerable<ISchemaDirective> directives, Models.EnumValue enumValue)
        {
            foreach (var directive in directives)
            {
                directive.ProcessEnumValue(enumValue);
            }
        }
    }
}
