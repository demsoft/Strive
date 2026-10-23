using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Strive.Extensions
{
    public static class ReturnUrls
    {
        /// <summary>
        ///     Where the provider sends the person back after paying: the page of the thing that was bought, on the address the
        ///     person came from (one installation has several addresses), if that is one of ours.
        /// </summary>
        /// <param name="kind">The first part of the path: s (session), u (creator), v (video)</param>
        public static string For(HttpRequest request, IConfiguration configuration, string kind, string id)
        {
            var allowed = (configuration["Cors:AllowedOrigins"] ?? "")
                .Split(new[] {',', ';', ' '}, StringSplitOptions.RemoveEmptyEntries)
                .Concat((configuration["App:ExtraHosts"] ?? "")
                    .Split(new[] {',', ';', ' '}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.StartsWith("http") ? x : "https://" + x))
                .Select(x => x.TrimEnd('/'))
                .ToList();

            var origin = request.Headers.Origin.ToString().TrimEnd('/');
            var home = allowed.Contains(origin, StringComparer.OrdinalIgnoreCase)
                ? origin
                : allowed.FirstOrDefault() ?? configuration["App:FrontendUrl"]?.TrimEnd('/') ?? "";

            return $"{home}/{kind}/{Uri.EscapeDataString(id)}";
        }
    }
}
