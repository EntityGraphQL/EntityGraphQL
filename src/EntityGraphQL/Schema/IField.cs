using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Represents a field in a Graph QL type. This can be a mutation field in the Mutation type
    /// </summary>
    public interface IField
    {
        IDictionary<string, ArgType> Arguments { get; }
        string Name { get; }
        string Description { get; }
        GqlTypeInfo ReturnType { get; }
        RequiredClaims AuthorizeClaims { get; }

        ArgType GetArgumentType(string argName);
        bool HasArgumentByName(string argName);
    }

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

    /// <summary>
    /// Holds information about arguments for fields
    /// </summary>
    public class ArgType
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public GqlTypeInfo Type { get; set; }

        public static ArgType FromProperty(ISchemaProvider schema, PropertyInfo prop)
        {
            var arg = MakeArgType(schema, prop.PropertyType, prop);

            return arg;
        }

        public static ArgType FromField(ISchemaProvider schema, FieldInfo field)
        {
            var arg = MakeArgType(schema, field.FieldType, field);

            return arg;
        }

        private static ArgType MakeArgType(ISchemaProvider schema, Type type, MemberInfo field)
        {
            var markedRequired = false;
            var typeToUse = type;
            if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(RequiredField<>))
            {
                markedRequired = true;
                typeToUse = type.GetGenericArguments()[0];
            }

            var arg = new ArgType
            {
                Type = new GqlTypeInfo(() => schema.Type(typeToUse.IsConstructedGenericType && typeToUse.GetGenericTypeDefinition() == typeof(EntityQueryType<>) ? typeof(string) : typeToUse.GetNonNullableOrEnumerableType()), typeToUse),
                Name = field.Name,
            };

            arg.Type.TypeNotNullable = GraphQLNotNullAttribute.IsMemberMarkedNotNull(field)
                || arg.Type.TypeNotNullable
                || field.GetCustomAttribute(typeof(RequiredAttribute), false) != null;
            if (markedRequired)
                arg.Type.TypeNotNullable = true;

            if (field.GetCustomAttribute(typeof(DescriptionAttribute), false) is DescriptionAttribute d)
            {
                arg.Description = d.Description;
            }

            return arg;
        }
    }
}