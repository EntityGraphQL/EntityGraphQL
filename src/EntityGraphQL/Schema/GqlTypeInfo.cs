using System;
using System.Reflection;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema;

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
    /// <param name="nullability">Nullability information about the property</param>
    public GqlTypeInfo(Func<ISchemaType> schemaTypeGetter, Type typeDotnet, NullabilityInfo? nullability)
    {
        SchemaTypeGetter = schemaTypeGetter;

        TypeDotnet = typeDotnet;
        IsList = TypeDotnet.IsEnumerableOrArray();
        TypeNotNullable = nullability != null ? nullability.ReadState == NullabilityState.NotNull : TypeDotnet.IsValueType && !TypeDotnet.IsNullableType();
        ElementTypeNullable = nullability != null && nullability.GenericTypeArguments.Length > 0 && nullability.GenericTypeArguments[0].ReadState == NullabilityState.Nullable;
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
    public string GqlTypeForReturnOrArgument =>
        $"{(IsList ? "[" : "")}{SchemaType.Name}{((!IsList && TypeNotNullable) || (IsList && !ElementTypeNullable) ? "!" : "")}{(IsList ? "]" : "")}{(IsList && TypeNotNullable ? "!" : "")}";

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

    /// <summary>
    /// A copy of this type info. A field's ReturnType is mutable - IsNullable() writes to it - so anything
    /// handing out a shared instance (e.g. a custom type mapping) must hand out a copy, or one field's
    /// IsNullable() call changes every other field using it.
    /// </summary>
    internal GqlTypeInfo Copy() =>
        new(SchemaTypeGetter, TypeDotnet)
        {
            TypeNotNullable = TypeNotNullable,
            IsList = IsList,
            ElementTypeNullable = ElementTypeNullable,
        };

    /// <summary>
    /// Whether a value of dotnetType is a list of schemaDotnetType. A list when its items are of the schema
    /// type, or its type of them, either way round (List&lt;Employee&gt; as Person, IEnumerable&lt;object&gt; as
    /// Child, List&lt;object&gt; as an object scalar). Not when the value is the schema type itself, or castable
    /// to it, though it is enumerable - a property bag (Dictionary&lt;string, object&gt;, or IDictionary) is a
    /// collection of its key/value pairs. Any other enumerable is a list, as it always was.
    /// </summary>
    internal static bool IsListOf(Type dotnetType, Type schemaDotnetType)
    {
        // the type itself first - a TreeNode : IEnumerable<TreeNode> named as TreeNode is one
        if (dotnetType == schemaDotnetType)
            return false;
        var elementType = dotnetType.GetCollectionElementType();
        if (elementType != null && CastCompatible(elementType, schemaDotnetType))
            return true;
        if (CastCompatible(dotnetType, schemaDotnetType))
            return false;
        return dotnetType.IsEnumerableOrArray();
    }

    /// <summary>
    /// Whether a value typed <paramref name="from"/> is cast to <paramref name="to"/> to read the fields of a schema
    /// type over it: only where it is typed object or a dictionary interface - a property bag behind IDictionary,
    /// or a value the expression could not type. Not a narrowing between real types (IAnimal to Dog), which
    /// would throw on another implementation and may not translate.
    /// </summary>
    internal static bool NeedsCastTo(Type from, Type to) =>
        from != to
        && from.IsAssignableFrom(to)
        && (from == typeof(object) || (from.IsInterface && from.IsGenericType && from.GetGenericTypeDefinition() is var d && (d == typeof(System.Collections.Generic.IDictionary<,>) || d == typeof(System.Collections.Generic.IReadOnlyDictionary<,>))));

    /// <summary>Whether a value of one type can be a value of the other: either is assignable from the other.</summary>
    internal static bool CastCompatible(Type a, Type b) => a.IsAssignableFrom(b) || b.IsAssignableFrom(a);

    public static GqlTypeInfo FromGqlType(ISchemaProvider schema, Type dotnetType, string gqlType)
    {
        var strippedType = gqlType.Trim('!').Trim('[').Trim(']').Trim('!');
        var typeInfo = new GqlTypeInfo(() => schema.Type(strippedType), dotnetType) { TypeNotNullable = gqlType.EndsWith('!'), IsList = gqlType.Contains('[', StringComparison.InvariantCulture) };
        typeInfo.ElementTypeNullable = !(typeInfo.IsList && gqlType.Trim('!').Trim('[').Trim(']').EndsWith('!'));

        return typeInfo;
    }
}
