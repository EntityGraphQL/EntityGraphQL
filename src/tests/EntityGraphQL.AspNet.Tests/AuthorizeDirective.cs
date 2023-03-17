using EntityGraphQL.Schema;
using EntityGraphQL.Schema.Directives;
using Microsoft.AspNetCore.Authorization;

namespace EntityGraphQL.AspNet.Tests
{
    public class AuthorizeAttributeHandler : IExtensionAttributeHandler
    {
        public IEnumerable<Type> AttributeTypes => new Type[] { typeof(AuthorizeAttribute), typeof(GraphQLAuthorizePolicyAttribute) };

        public void ApplyExtension(IField field, Attribute attribute)
        {
            if (attribute is AuthorizeAttribute authAttribute)
            {
                field.AddDirective(new AuthorizeDirective(authAttribute));
            }
            else if (attribute is GraphQLAuthorizePolicyAttribute policyAttribute)
            {
                field.AddDirective(new AuthorizeDirective(policyAttribute));
            }
        }

        public void ApplyExtension(ISchemaType type, Attribute attribute)
        {
            if (attribute is AuthorizeAttribute authAttribute)
            {
                type.AddDirective(new AuthorizeDirective(authAttribute));
            }
            else if (attribute is GraphQLAuthorizePolicyAttribute policyAttribute)
            {
                type.AddDirective(new AuthorizeDirective(policyAttribute));
            }
        }
    }

    public class AuthorizeDirective : ISchemaDirective
    {
        public AuthorizeDirective(AuthorizeAttribute authorize)
        {
            Roles = authorize.Roles;
            Policies = new List<string>() { authorize.Policy! };
        }

        public AuthorizeDirective(GraphQLAuthorizePolicyAttribute authorize)
        {
            Policies = authorize.Policies;
        }

        public IEnumerable<TypeSystemDirectiveLocation> Location => new[] {
            TypeSystemDirectiveLocation.QueryObject,
            TypeSystemDirectiveLocation.FieldDefinition
        };

        public string? Roles { get; }
        public List<string> Policies { get; }

        public string ToGraphQLSchemaString()
        {
            return $"@authorize(roles: \"{Roles}\", policies: \"{string.Join(", ", Policies)}\")";
        }
    }
}