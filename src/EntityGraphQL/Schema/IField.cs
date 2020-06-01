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
        public GqlTypeInfo(ISchemaType schemaType, Type typeDotnet)
        {
            SchemaType = schemaType;
            TypeDotnet = typeDotnet;
            TypeNotNullable = typeDotnet.GetTypeInfo().IsValueType && !typeDotnet.IsNullableType();
        }

        /// <summary>
        /// The schema type
        /// </summary>
        /// <value></value>
        public ISchemaType SchemaType { get; }
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
        public bool IsList => TypeDotnet.IsEnumerableOrArray();
        /// <summary>
        /// Mapped type in dotnet
        /// </summary>
        /// <value></value>
        public Type TypeDotnet { get; set; }

        public override string ToString()
        {
            return GqlTypeForReturnOrArgument;
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
            var arg = new ArgType
            {
                Type = new GqlTypeInfo(schema.Type(type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(EntityQueryType<>) ? typeof(string) : type.GetNonNullableOrEnumerableType()), type),
                Name = field.Name,
            };
            arg.Type.TypeNotNullable = GraphQLNotNullAttribute.IsMemberMarkedNotNull(field)
                || arg.Type.TypeNotNullable
                || field.GetCustomAttribute(typeof(RequiredAttribute), false) != null;

            if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(RequiredField<>))
                arg.Type.TypeNotNullable = true;

            if (field.GetCustomAttribute(typeof(DescriptionAttribute), false) is DescriptionAttribute d)
            {
                arg.Description = d.Description;
            }

            return arg;
        }
    }
}