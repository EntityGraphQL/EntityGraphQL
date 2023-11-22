using System;

namespace EntityGraphQL.Schema;

/// <summary>
/// Wraps up the mutation fields so we can treat this like any other type
/// </summary>
public class MutationSchemaType : BaseSchemaTypeWithFields<MutationField>
{
    public override Type TypeDotnet => typeof(MutationType);
    public MutationSchemaType(ISchemaProvider schema, string name, string? description, RequiredAuthorization? requiredAuthorization)
        : base(schema, name, description, requiredAuthorization)
    {
        GqlType = GqlTypes.Mutation;
    }

    public override ISchemaType AddAllFields(SchemaBuilderOptions? options = null)
    {
        return this;
    }

    public override ISchemaType ImplementAllBaseTypes(bool addTypeIfNotInSchema = true, bool addAllFieldsOnAddedType = true)
    {
        throw new EntityQuerySchemaException("Cannot add base types to a mutation");
    }
    public override ISchemaType Implements<TClrType>(bool addTypeIfNotInSchema = true, bool addAllFieldsOnAddedType = true)
    {
        throw new EntityQuerySchemaException("Cannot add base types to a mutation");
    }
    public override ISchemaType Implements(string typeName)
    {
        throw new EntityQuerySchemaException("Cannot add base types to a mutation");
    }
}