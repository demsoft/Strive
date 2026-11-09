#nullable enable
using System;
using Microsoft.AspNetCore.Http;

namespace Identity.API
{
    /// <summary>
    ///     Which brand the person is signing in to. One installation serves several addresses: gbedu.example.com is
    ///     Gbedu (live music sessions), arena.example.com is Arena (football matches), everything else is Strive. The pages take their name, colours and text from it.
    /// </summary>
    public class BrandInfo
    {
        public const string Strive = "strive";
        public const string Gbedu = "gbedu";
        public const string Arena = "arena";

        private readonly IHttpContextAccessor? _accessor;

        public BrandInfo(IHttpContextAccessor? accessor = null)
        {
            _accessor = accessor;
        }

        public string Id => ForHost(_accessor?.HttpContext?.Request.Host.Host);

        public string Name => Id == Arena ? "Arena" : Id == Gbedu ? "Gbedu" : "Strive";

        /// <summary>A creator platform (Gbedu, and Arena which is built on it) and not the meetings app</summary>
        public bool IsGbedu => Id != Strive;

        public bool IsArena => Id == Arena;

        public static string ForHost(string? host)
        {
            if (host == null) return Strive;
            if (host.StartsWith("gbedu.", StringComparison.OrdinalIgnoreCase)) return Gbedu;
            return host.StartsWith("arena.", StringComparison.OrdinalIgnoreCase) ? Arena : Strive;
        }
    }
}
