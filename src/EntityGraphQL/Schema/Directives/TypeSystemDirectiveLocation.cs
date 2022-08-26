using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityGraphQL.Schema.Directives
{
    public enum TypeSystemDirectiveLocation
    {
        SCHEMA,
        SCALAR,
        OBJECT,
        FIELD_DEFINITION,
        ARGUMENT_DEFINITION,
        INTERFACE,
        UNION,
        ENUM,
        ENUM_VALUE,
        INPUT_OBJECT,
        INPUT_FIELD_DEFINITION
    }
}