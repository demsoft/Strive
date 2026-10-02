using System;

namespace Strive.Core.Services.Media
{
    /// <summary>
    ///     Configuration of the TURN server (coturn) that relays media for participants that cannot reach the SFU directly,
    ///     for example behind strict firewalls. TURN is disabled if no urls are configured.
    /// </summary>
    public class TurnOptions
    {
        /// <summary>
        ///     The ice server urls handed to the browsers, e.g. "turn:turn.example.com:3478?transport=udp",
        ///     "turn:turn.example.com:3478?transport=tcp" or "turns:turn.example.com:443?transport=tcp"
        /// </summary>
        public string[] Urls { get; set; } = Array.Empty<string>();

        /// <summary>
        ///     The shared secret of the TURN server (static-auth-secret in the coturn configuration)
        /// </summary>
        public string? Secret { get; set; }

        /// <summary>
        ///     How long the generated credentials are valid. A credential is only checked when an allocation is created or
        ///     refreshed, and clients fetch new credentials whenever they connect to the SFU.
        /// </summary>
        public TimeSpan CredentialLifetime { get; set; } = TimeSpan.FromHours(24);

        public bool IsEnabled => Urls.Length > 0 && !string.IsNullOrEmpty(Secret);
    }
}
