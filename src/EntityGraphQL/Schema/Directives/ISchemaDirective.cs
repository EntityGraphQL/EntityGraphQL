using System.Collections.Generic;

namespace EntityGraphQL.Schema.Directives
{
    public interface ISchemaDirective
    {
        IEnumerable<TypeSystemDirectiveLocation> Location { get; }

        void ProcessField(Models.Field field) { }
        void ProcessType(Models.TypeElement type) { }
        void ProcessEnumValue(Models.EnumValue enumValue) { }

        string ToGraphQLSchemaString();
    }
}