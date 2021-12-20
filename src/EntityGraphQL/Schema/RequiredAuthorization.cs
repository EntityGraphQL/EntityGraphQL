using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityGraphQL.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace EntityGraphQL.Schema
{
    /// <summary>
    /// Details on the authorisation required by a field or type
    /// </summary>
    public class RequiredAuthorization
    {
        /// <summary>
        /// Each item in the "first" list is AND claims and each in the inner list is OR claims
        /// This means [Authorize(Roles = "Blah,Blah2")] is either of those roles
        /// and
        /// [Authorize(Roles = "Blah")]
        /// [Authorize(Roles = "Blah2")] is both of those roles
        /// </summary>
        private readonly List<List<string>> requiredPolicies;
        public IEnumerable<IEnumerable<string>> Policies { get => requiredPolicies; }
        private readonly List<List<string>> requiredRoles;
        public IEnumerable<IEnumerable<string>> Roles { get => requiredRoles; }

        public RequiredAuthorization()
        {
            requiredPolicies = new List<List<string>>();
            requiredRoles = new List<List<string>>();
        }

        public RequiredAuthorization(IEnumerable<GraphQLAuthorizeAttribute> claims, IEnumerable<AuthorizeAttribute> authorizeAttributes)
        {
            requiredRoles = claims.Select(c => c.Claims).ToList();
            requiredRoles = requiredRoles.Concat(authorizeAttributes.Select(c => c.Roles?.Split(",").ToList())).Where(l => l != null).ToList();
            requiredPolicies = authorizeAttributes.Select(c => c.Policy.Split(",").ToList()).ToList();
        }

        public static RequiredAuthorization GetRequiredAuthFromExpression(LambdaExpression fieldSelection)
        {
            RequiredAuthorization requiredAuth = null;
            if (fieldSelection.Body.NodeType == ExpressionType.MemberAccess)
            {
                var attributes = ((MemberExpression)fieldSelection.Body).Member.GetCustomAttributes(typeof(GraphQLAuthorizeAttribute), true).Cast<GraphQLAuthorizeAttribute>();
                var auths = ((MemberExpression)fieldSelection.Body).Member.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>();
                requiredAuth = new RequiredAuthorization(attributes, auths);
            }

            return requiredAuth;
        }
        public static RequiredAuthorization GetRequiredAuthFromField(MemberInfo field)
        {
            var attributes = field.GetCustomAttributes(typeof(GraphQLAuthorizeAttribute), true).Cast<GraphQLAuthorizeAttribute>();
            var auths = field.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>();
            var requiredAuth = new RequiredAuthorization(attributes, auths);
            return requiredAuth;
        }

        public static RequiredAuthorization GetRequiredAuthFromType(Type type)
        {
            var attributes = type.GetCustomAttributes(typeof(GraphQLAuthorizeAttribute), true).Cast<GraphQLAuthorizeAttribute>();
            var auths = type.GetCustomAttributes(typeof(AuthorizeAttribute)).Cast<AuthorizeAttribute>();

            var requiredAuth = new RequiredAuthorization(attributes, auths);
            return requiredAuth;
        }

        public bool Any() => requiredPolicies.Any() || requiredRoles.Any();

        public void RequiresAnyRole(params string[] roles)
        {
            requiredRoles.Add(roles.ToList());
        }

        public void RequiresAllRoles(params string[] roles)
        {
            requiredRoles.AddRange(roles.Select(s => new List<string> { s }));
        }

        public void RequiresAnyPolicy(params string[] policies)
        {
            requiredPolicies.Add(policies.ToList());
        }

        public void RequiresAllPolicies(params string[] policies)
        {
            requiredPolicies.AddRange(policies.Select(s => new List<string> { s }));
        }
    }
}