using System;
using System.Collections.Generic;
using Strive.Core.Services.Synchronization;

namespace Strive.Core.Services.Lobby
{
    /// <summary>
    ///     The participants that wait to be admitted. Only visible to participants that are allowed to admit.
    /// </summary>
    public record SynchronizedLobby(IReadOnlyDictionary<string, SynchronizedLobby.Waiting> Participants)
    {
        public static readonly SynchronizedObjectId SyncObjId = new(SynchronizedObjectIds.LOBBY);

        public record Waiting(string DisplayName, DateTimeOffset Since);
    }
}
