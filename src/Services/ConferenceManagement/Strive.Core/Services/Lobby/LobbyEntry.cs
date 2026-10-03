using System;

namespace Strive.Core.Services.Lobby
{
    /// <summary>
    ///     A participant waiting in the lobby
    /// </summary>
    /// <param name="ConnectionId">The connection that waits, it will be joined if the participant is admitted</param>
    public record LobbyEntry(string ConnectionId, string DisplayName, DateTimeOffset Since);
}
