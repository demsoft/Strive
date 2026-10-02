using System.Collections.Generic;

namespace Strive.Core.Services.Media
{
    /// <param name="Url">The url of the SFU</param>
    /// <param name="AuthToken">The token to authenticate at the SFU</param>
    /// <param name="IceServers">STUN/TURN servers that the client should use, empty if there are none</param>
    public record SfuConnectionInfo(string Url, string AuthToken, IReadOnlyList<IceServerInfo> IceServers);

    /// <summary>
    ///     An ice server in the format of RTCIceServer
    /// </summary>
    public record IceServerInfo(IReadOnlyList<string> Urls, string? Username, string? Credential);
}
