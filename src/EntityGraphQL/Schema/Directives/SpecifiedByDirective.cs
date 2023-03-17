using EntityGraphQL.Schema.Directives;
using System;
using System.Collections.Generic;

namespace EntityGraphQL.Schema
{
    public static class SpecifiedByExtensions
    {
        public static void SpecifiedBy(this ISchemaType type, string url)
        {
            if (!type.IsScalar)
            {
                throw new EntityQuerySchemaException($"@specifiedBy can only be used on scalars");
            }

            type.AddDirective(new SpecifiedByDirective(url));
        }
    }
}

namespace EntityGraphQL.Schema.Directives
{

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class SpecifiedByDirectiveAttribute : ExtensionAttribute
    {
        public override void ApplyExtension(ISchemaType type)
        {
            type.OneOf();
        }
    }

    public class SpecifiedByDirective : ISchemaDirective
    {
        public SpecifiedByDirective(string url)
        {
            this.Url = url;
        }

        public IEnumerable<TypeSystemDirectiveLocation> Location => new[] {
            TypeSystemDirectiveLocation.Scalar
        };

        public void ProcessType(Models.TypeElement type)
        {
            type.SpecifiedByURL = Url;
        }

        public string Url { get; }

        public string ToGraphQLSchemaString()
        {
            return $"@specifiedBy(url: \"{Url}\")";
        }
    }
}