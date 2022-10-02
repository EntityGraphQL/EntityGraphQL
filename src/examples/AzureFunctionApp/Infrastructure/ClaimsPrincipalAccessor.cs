using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace demo.Infrastructure
{
    public class ClaimsPrincipalAccessor
    {
        private IHttpContextAccessor _contextAccessor;

        public ClaimsPrincipalAccessor(IHttpContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
        }

        private class ClientPrincipal
        {
            public string IdentityProvider { get; set; } = String.Empty;
            public string UserId { get; set; } = String.Empty;
            public string UserDetails { get; set; } = String.Empty;
            public IEnumerable<string> UserRoles { get; set; } = new List<string>();
        }

        public ClaimsPrincipal GetClaimsPrincipal()
        {
            var context = _contextAccessor.HttpContext;
            var req = context?.Request;
            var principal = new ClientPrincipal();

            if (req?.Headers?.TryGetValue("x-ms-client-principal", out var header) ?? false)
            {
                var data = header[0];
                var decoded = Convert.FromBase64String(data);
                var json = Encoding.UTF8.GetString(decoded);
                principal = JsonSerializer.Deserialize<ClientPrincipal>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (principal == null)
                    throw new BadHttpRequestException("Failed to parse client-principal");

                principal.UserRoles = principal.UserRoles?.Except(new string[] { "anonymous" }, StringComparer.CurrentCultureIgnoreCase) ?? new List<String>();

                var identity = new ClaimsIdentity(principal.IdentityProvider);
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, principal.UserId));

                return new ClaimsPrincipal(identity);
            }

            return new ClaimsPrincipal();
        }
    }
}
