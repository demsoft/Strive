// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace Identity.API
{
    public static class Config
    {
        public static IEnumerable<IdentityResource> IdentityResources =>
            new[]
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
                new IdentityResource {Name = "user.info", UserClaims = {ClaimTypes.Role, ClaimTypes.Name, JwtClaimTypes.Role}},
            };

        /// <param name="host">The address of the web app, e.g. https://meet.example.com</param>
        /// <param name="extraHosts">More addresses that serve the same web app (another brand on its own domain)</param>
        public static Client BuildSpaClient(string host, IEnumerable<string>? extraHosts = null)
        {
            var hosts = new[] {host}.Concat(extraHosts ?? Enumerable.Empty<string>()).Distinct().ToArray();
            return new()
            {
                ClientId = "spa", AllowedGrantTypes = GrantTypes.Code,
                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId, IdentityServerConstants.StandardScopes.Profile,
                    "user.info",
                },
                RedirectUris = hosts.SelectMany(h => new[]
                    {$"{h}/authentication/callback", $"{h}/authentication/silent_callback"}).ToList(),
                PostLogoutRedirectUris = hosts.Select(h => $"{h}/").ToList(),
                AllowedCorsOrigins = hosts.ToList(),
                RequireClientSecret = false,
            };
        }
    }
}
