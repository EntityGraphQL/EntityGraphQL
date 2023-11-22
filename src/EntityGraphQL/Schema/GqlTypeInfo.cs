using System;
using System.Reflection;
using EntityGraphQL.Extensions;
#if NETSTANDARD2_1
using Nullability;
#endif

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Holds information about a type result in the schema (e.g. a field return type)
    /// </summary>
    public class GqlTypeInfo
    {
        private ISchemaType? schemaType;

        /// <summary>
        /// New GqlTypeInfo object that represents information about the return/argument type
        /// </summary>
        /// <param name="schemaTypeGetter">Func to get the ISchemaType. Lookup is func as the type might be added later. It is cached after first look up</param>
        /// <param name="typeDotnet">The dotnet type as it is. E.g. the List<T> etc. </param>
        public GqlTypeInfo(Func<ISchemaType> schemaTypeGetter, Type typeDotnet)
        {
            SchemaTypeGetter = schemaTypeGetter;

            TypeDotnet = typeDotnet;
            IsList = TypeDotnet.IsEnumerableOrArray();
            TypeNotNullable = TypeDotnet.IsValueType && !TypeDotnet.IsNullableType();
            ElementTypeNullable = false;
        }

        /// <summary>
        /// New GqlTypeInfo object that represents information about the return/argument type
        /// </summary>
        /// <param name="schemaTypeGetter">Func to get the ISchemaType. Lookup is func as the type might be added later. It is cached after first look up</param>
        /// <param name="typeDotnet">The dotnet type as it is. E.g. the List<T> etc. </param>
        /// <param name="nullability">Nullability infomation about the property</param>
        public GqlTypeInfo(Func<ISchemaType> schemaTypeGetter, Type typeDotnet, NullabilityInfo nullability)
        {
            SchemaTypeGetter = schemaTypeGetter;

            TypeDotnet = typeDotnet;
            IsList = TypeDotnet.IsEnumerableOrArray();
            TypeNotNullable = nullability.ReadState == NullabilityState.NotNull;
            ElementTypeNullable = nullability.GenericTypeArguments.Length > 0 && nullability.GenericTypeArguments[0].ReadState == NullabilityState.Nullable;
        }

        /// <summary>
        /// The schema type
        /// </summary>
        /// <value></value>
        public ISchemaType SchemaType
        {
            get
            {
                schemaType ??= SchemaTypeGetter();
                return schemaType;
            }
        }
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
                TypeNotNullable = gqlType.EndsWith("!", StringComparison.InvariantCulture),
                IsList = gqlType.Contains('[', StringComparison.InvariantCulture),
            };
            typeInfo.ElementTypeNullable = !(typeInfo.IsList && gqlType.Trim('!').Trim('[').Trim(']').EndsWith("!", StringComparison.InvariantCulture));

            return typeInfo;
        }
    }
}