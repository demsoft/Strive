#nullable enable
using Microsoft.Extensions.Configuration;

namespace Identity.API
{
    /// <summary>
    ///     Where the web app is. The identity pages are served under /account of the same host in production, so a link
    ///     to "/" would lead to the identity service itself: links back to the app have to go to the address of the app.
    /// </summary>
    public class AppUrls
    {
        public AppUrls(IConfiguration configuration)
        {
            var host = configuration["IdentityServer:SpaClientHost"];
            Home = string.IsNullOrWhiteSpace(host) ? "/" : host.TrimEnd('/') + "/";
        }

        public string Home { get; }
    }
}
