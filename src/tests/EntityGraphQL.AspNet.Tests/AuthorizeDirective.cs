using EntityGraphQL.AspNet;
using EntityGraphQL.Schema.Directives;
using EntityGraphQL.Schema.Models;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EntityGraphQL.Schema.Directives
{
    public class AuthorizeAttributeRegistration :
        IExtensionAttribute<AuthorizeAttribute>,
        IExtensionAttribute<GraphQLAuthorizePolicyAttribute>
    {
        public void ApplyExtension(IField field, AuthorizeAttribute attribute)
        {
            field.AddDirective(new AuthorizeDirective(attribute));
        }

        public void ApplyExtension(IField field, GraphQLAuthorizePolicyAttribute attribute)
        {
            field.AddDirective(new AuthorizeDirective(attribute));
        }

        public void ApplyExtension(ISchemaType type, AuthorizeAttribute attribute)
        {
            type.AddDirective(new AuthorizeDirective(attribute));
        }

        public void ApplyExtension(ISchemaType type, GraphQLAuthorizePolicyAttribute attribute)
        {
            type.AddDirective(new AuthorizeDirective(attribute));
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

        public IEnumerable<TypeSystemDirectiveLocation> On => new[] {
            TypeSystemDirectiveLocation.OBJECT,
            TypeSystemDirectiveLocation.FIELD_DEFINITION
        };

        public string? Roles { get; }
        public List<string> Policies { get; }

        public string ToGraphQLSchemaString()
        {
            return $"@authorize(roles: \"{Roles}\", policies: \"{string.Join(", ", Policies)}\")";
        }
    }
}