using System;
using System.Reflection;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Holds information about a type result in the schema (e.g. a field return type)
    /// </summary>
    public class GqlTypeInfo
    {
        private readonly bool nullableValueTypes;

        /// <summary>
        /// New GqlTypeInfo object that represents information about the return/argument type
        /// </summary>
        /// <param name="schemaTypeGetter">Func to get the ISchemaType</param>
        /// <param name="typeDotnet">The dotnet type as it is</param>
        /// <param name="nullableValueTypes">value types are nullable. Used for arguments where they may have default values</param>
        public GqlTypeInfo(Func<ISchemaType> schemaTypeGetter, Type typeDotnet, bool nullableValueTypes = false)
        {
            SchemaTypeGetter = schemaTypeGetter;
            TypeDotnet = typeDotnet;
            this.nullableValueTypes = nullableValueTypes;
            Init();
        }

        private void Init()
        {
            TypeNotNullable = !nullableValueTypes && TypeDotnet.GetTypeInfo().IsValueType && !TypeDotnet.IsNullableType();
            IsList = TypeDotnet.IsEnumerableOrArray();
        }

        /// <summary>
        /// The schema type
        /// </summary>
        /// <value></value>
        public ISchemaType SchemaType => SchemaTypeGetter();
        /// <summary>
        /// Type described as type as a full GraphQL type. e.g. [Int!]!
        /// </summary>
        /// <value></value>
        public string GqlTypeForReturnOrArgument => $"{(IsList ? "[" : "")}{SchemaType.Name}{((!IsList && TypeNotNullable) || (IsList && !ElementTypeNullable) ? "!" : "")}{(IsList ? "]" : "")}{(IsList && TypeNotNullable ? "!" : "")}";
        /// <summary>
        /// Typw is not nullable (! in GQL)
        /// </summary>
        /// <value></value>
        public bool TypeNotNullable { get; set; }
        /// <summary>
        /// If IsList the element type if nullable or not ([Type!] in gql)
        /// </summary>
        /// <value></value>
        public bool ElementTypeNullable { get; set; }
        /// <summary>
        /// The Type is a list/array ([] in gql)
        /// </summary>
        /// <value></value>
        public bool IsList { get; set; }

        public Func<ISchemaType> SchemaTypeGetter { get; }

        /// <summary>
        /// Mapped type in dotnet
        /// </summary>
        /// <value></value>
        public Type TypeDotnet { get; }

        public override string ToString()
        {
            return GqlTypeForReturnOrArgument;
        }

        public static GqlTypeInfo FromGqlType(ISchemaProvider schema, Type dotnetType, string gqlType)
        {
            var strippedType = gqlType.Trim('!').Trim('[').Trim(']').Trim('!');
            var typeInfo = new GqlTypeInfo(() => schema.Type(strippedType), dotnetType)
            {
                TypeNotNullable = gqlType.EndsWith("!"),
                IsList = gqlType.Contains("["),
            };
            typeInfo.ElementTypeNullable = !(typeInfo.IsList && gqlType.Trim('!').Trim('[').Trim(']').EndsWith("!"));

            return typeInfo;
        }
    }
}