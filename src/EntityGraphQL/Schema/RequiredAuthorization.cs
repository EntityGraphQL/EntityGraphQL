using System.Collections.Generic;
using System.Linq;

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

        /// <summary>
        /// Create a new RequiredAuthorization object from a list of roles and/or policies
        /// </summary>
        /// <param name="roles">Roles required</param>
        /// <param name="policies">ASP.NET policies requried</param>
        public RequiredAuthorization(IEnumerable<List<string>>? roles, IEnumerable<List<string>>? policies)
        {
            requiredRoles = roles?.ToList() ?? new List<List<string>>();
            requiredPolicies = policies?.ToList() ?? new List<List<string>>();
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

        public RequiredAuthorization Concat(RequiredAuthorization requiredAuthorization)
        {
            var newRequiredAuthorization = new RequiredAuthorization();
            newRequiredAuthorization.requiredPolicies.AddRange(requiredPolicies);
            newRequiredAuthorization.requiredPolicies.AddRange(requiredAuthorization.requiredPolicies);
            newRequiredAuthorization.requiredRoles.AddRange(requiredRoles);
            newRequiredAuthorization.requiredRoles.AddRange(requiredAuthorization.requiredRoles);
            return newRequiredAuthorization;
        }
    }
}