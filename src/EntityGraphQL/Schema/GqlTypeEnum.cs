namespace EntityGraphQL.Schema;

public enum GqlTypes
{
    Scalar,
    Enum,
    QueryObject,
    Interface,
    InputObject,
    Mutation,
    Union,
}

public static class GqlTypesExtensions
{
    public static bool IsNotValidForInput(this GqlTypes type)
    {
        return type == GqlTypes.Interface || type == GqlTypes.Mutation || type == GqlTypes.QueryObject || type == GqlTypes.Union;
    }
}
