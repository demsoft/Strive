using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Strive.Auth
{
    /// <summary>
    /// Narrow handoff contract for the HRM adapter. Not registered as a general API authentication scheme.
    /// The HRM issuer must check current organization membership and call access before signing a grant.
    /// </summary>
    public sealed class HrmCallGrantValidator
    {
        public const string Audience = "goshrm-embedded-call";
        public const string Purpose = "call.join";
        private readonly string _issuer;
        private readonly SecurityKey _publicKey;

        public HrmCallGrantValidator(string issuer, SecurityKey publicKey)
        {
            if (!Uri.TryCreate(issuer, UriKind.Absolute, out var uri) || uri.Scheme != "https")
                throw new ArgumentException("An HTTPS HRM issuer is required.", nameof(issuer));
            _issuer = issuer;
            _publicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
        }

        public HrmCallGrant Validate(string token, string expectedOrganization, string expectedConference,
            DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(expectedOrganization) || string.IsNullOrWhiteSpace(expectedConference))
                throw new SecurityTokenValidationException("Call scope is required.");

            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false, MaximumTokenSizeInBytes = 8192 };
            ClaimsPrincipal principal;
            try
            {
                principal = handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidIssuer = _issuer,
                    ValidAudience = Audience,
                    IssuerSigningKey = _publicKey,
                    ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    LifetimeValidator = (notBefore, expires, _, _) =>
                        notBefore.HasValue && expires.HasValue &&
                        notBefore.Value <= now.UtcDateTime && expires.Value > now.UtcDateTime &&
                        expires.Value > notBefore.Value && expires.Value - notBefore.Value <= TimeSpan.FromMinutes(2),
                }, out _);
            }
            catch (ArgumentException exception)
            {
                throw new SecurityTokenValidationException("Call grant is malformed.", exception);
            }

            string One(string name)
            {
                var values = principal.FindAll(name).ToArray();
                if (values.Length != 1 || string.IsNullOrWhiteSpace(values[0].Value))
                    throw new SecurityTokenValidationException("Call grant claim is missing or ambiguous.");
                return values[0].Value;
            }

            var organization = One("organization_id");
            var conference = One("conference_id");
            if (organization != expectedOrganization || conference != expectedConference || One("purpose") != Purpose)
                throw new SecurityTokenValidationException("Call grant scope does not match.");

            // Never turn a bridge token into a principal carrying supplied roles or recorder privileges.
            if (principal.Claims.Any(c => c.Type == "role" || c.Type == "roles" ||
                                          c.Type == System.Security.Claims.ClaimTypes.Role))
                throw new SecurityTokenValidationException("Call grants cannot carry roles.");

            return new HrmCallGrant(One("sub"), organization, conference, One(JwtRegisteredClaimNames.Jti));
        }
    }

    public sealed record HrmCallGrant(string Subject, string OrganizationId, string ConferenceId, string GrantId);
}
