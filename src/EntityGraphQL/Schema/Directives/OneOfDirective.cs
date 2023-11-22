using EntityGraphQL.Compiler;
using EntityGraphQL.Schema.Directives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL.Schema
{
    public static class OneOfDirectiveExtensions
    {
        public static void OneOf(this ISchemaType type)
        {
            type.AddDirective(new OneOfDirective());

            type.OnAddField += (field) =>
            {
                if (field.ReturnType.TypeNotNullable)
                {
                    throw new EntityQuerySchemaException($"{type.TypeDotnet.Name} is a OneOf type but all its fields are not nullable. OneOf input types require all the field to be nullable.");
                }
            };

            type.OnValidate += (value) =>
            {
                if (value != null)
                {
                    var singleField = value.GetType().GetProperties().Count(x => x.GetValue(value) != null);

                    if (singleField != 1) // we got multiple set
                        throw new EntityGraphQLValidationException($"Exactly one field must be specified for argument of type {type.Name}.");
                }
            };

        }
    }
}

namespace EntityGraphQL.Schema.Directives
{

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class GraphQLOneOfAttribute : ExtensionAttribute
    {
        public override void ApplyExtension(ISchemaType type)
        {
            type.OneOf();
        }
    }

    public class OneOfDirective : ISchemaDirective
    {
        public OneOfDirective()
        {
        }

        public IEnumerable<TypeSystemDirectiveLocation> Location => new[] {
            TypeSystemDirectiveLocation.InputObject
        };

        public void ProcessType(Models.TypeElement type)
        {
            type.OneField = true;
        }

        public string ToGraphQLSchemaString()
        {
            return $"@oneOf";
        }
    }
}