#nullable enable
using System;
using Microsoft.AspNetCore.Http;

namespace Identity.API
{
    /// <summary>
    ///     Which brand the person is signing in to. One installation serves several addresses: gbedu.example.com is
    ///     Gbedu (live music sessions), everything else is Strive. The pages take their name, colours and text from it.
    /// </summary>
    public class BrandInfo
    {
        public const string Strive = "strive";
        public const string Gbedu = "gbedu";

        private readonly IHttpContextAccessor? _accessor;

        public BrandInfo(IHttpContextAccessor? accessor = null)
        {
            _accessor = accessor;
        }

        public string Id => ForHost(_accessor?.HttpContext?.Request.Host.Host);

        public string Name => Id == Gbedu ? "Gbedu" : "Strive";

        public bool IsGbedu => Id == Gbedu;

        public static string ForHost(string? host)
        {
            return host != null && host.StartsWith("gbedu.", StringComparison.OrdinalIgnoreCase) ? Gbedu : Strive;
        }
    }
}
