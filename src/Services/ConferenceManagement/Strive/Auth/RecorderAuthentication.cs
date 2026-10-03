using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Strive.Infrastructure.Recording;

namespace Strive.Auth
{
    /// <summary>
    ///     Recorders do not sign in at the identity server. They join with a token the server created for one recording
    ///     (see JwtRecorderJoinTokenFactory), validated by a separate authentication scheme.
    /// </summary>
    public static class RecorderAuthentication
    {
        public const string Scheme = "Recorder";
        public const string SmartScheme = "StriveSmart";

        public static TokenValidationParameters CreateValidationParameters(string? secret)
        {
            // without a secret no recorder token is valid (a random key nobody knows)
            var key = string.IsNullOrEmpty(secret) || secret.Length < 32
                ? RandomNumberGenerator.GetBytes(32)
                : Encoding.UTF8.GetBytes(secret);

            return new TokenValidationParameters
            {
                ValidIssuer = RecorderOptions.TokenIssuer,
                ValidAudience = RecorderOptions.TokenAudience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1),
            };
        }

        /// <summary>
        ///     Choose the scheme by the issuer of the token (not validated here, the chosen scheme validates it)
        /// </summary>
        public static string SelectScheme(HttpContext context)
        {
            var token = GetToken(context);
            if (token == null) return JwtBearerDefaults.AuthenticationScheme;

            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token)) return JwtBearerDefaults.AuthenticationScheme;

            try
            {
                return handler.ReadJwtToken(token).Issuer == RecorderOptions.TokenIssuer
                    ? Scheme
                    : JwtBearerDefaults.AuthenticationScheme;
            }
            catch (ArgumentException)
            {
                return JwtBearerDefaults.AuthenticationScheme;
            }
        }

        private static string? GetToken(HttpContext context)
        {
            var header = context.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(header) && AuthenticationHeaderValue.TryParse(header, out var value) &&
                string.Equals(value.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
                return value.Parameter;

            var query = context.Request.Query["access_token"].ToString();
            return string.IsNullOrEmpty(query) ? null : query;
        }
    }
}
