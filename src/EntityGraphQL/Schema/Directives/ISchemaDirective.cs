using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityGraphQL.Schema.Directives
{
    public interface ISchemaDirective
    {
        IEnumerable<TypeSystemDirectiveLocation> On { get; }

        void ProcessField(Models.Field field) { }
        void ProcessType(Models.TypeElement type) { }
        void ProcessEnumValue(Models.EnumValue enumValue) { }

        string ToGraphQLSchemaString();
    }
}