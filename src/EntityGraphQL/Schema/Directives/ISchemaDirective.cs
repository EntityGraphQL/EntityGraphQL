using System.Collections.Generic;

namespace EntityGraphQL.Schema.Directives
{
    public interface ISchemaDirective
    {
#pragma warning disable CA1716
        IEnumerable<TypeSystemDirectiveLocation> On { get; }
#pragma warning restore CA1716

        void ProcessField(Models.Field field) { }
        void ProcessType(Models.TypeElement type) { }
        void ProcessEnumValue(Models.EnumValue enumValue) { }

        string ToGraphQLSchemaString();
    }
}