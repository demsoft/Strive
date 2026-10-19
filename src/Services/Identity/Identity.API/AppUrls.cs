#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Identity.API
{
    /// <summary>
    ///     Where the web app is. The identity pages are served under /account of the same host in production, so a link
    ///     to "/" would lead to the identity service itself: links back to the app have to go to the address of the app.
    ///     The web app may answer on more addresses (IdentityServer:ExtraSpaHosts, one per brand): the sign in pages
    ///     then link back to the address the person came from.
    /// </summary>
    public class AppUrls
    {
        private readonly string _home;
        private readonly IHttpContextAccessor? _accessor;

        public AppUrls(IConfiguration configuration, IHttpContextAccessor? accessor = null)
        {
            var host = configuration["IdentityServer:SpaClientHost"];
            _home = string.IsNullOrWhiteSpace(host) ? "/" : host.TrimEnd('/') + "/";
            ExtraHosts = ParseExtraHosts(configuration["IdentityServer:ExtraSpaHosts"]);
            _accessor = accessor;
        }

        /// <summary>The origins (https://name) of the extra addresses of the web app</summary>
        public IReadOnlyList<string> ExtraHosts { get; }

        public string Home
        {
            get
            {
                var request = _accessor?.HttpContext?.Request;
                if (request != null)
                {
                    var origin = $"https://{request.Host.Value}";
                    if (ExtraHosts.Contains(origin, StringComparer.OrdinalIgnoreCase)) return origin + "/";
                }

                return _home;
            }
        }

        /// <summary>"gbedu.example.com, other.example.com" becomes the origins of those hosts</summary>
        public static IReadOnlyList<string> ParseExtraHosts(string? hosts)
        {
            return (hosts ?? "").Split(new[] {',', ';', ' '}, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().TrimEnd('/'))
                .Where(x => x.Length > 0)
                .Select(x => x.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? x : "https://" + x)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
