using System.Collections.Generic;
using System.Threading.Tasks;
using Strive.Core.Interfaces.Gateways;

namespace Strive.Core.Services.Lobby.Gateways
{
    public interface ILobbyRepository : IStateRepository
    {
        /// <summary>
        ///     Let the participant wait in the lobby. Replaces the entry if the participant already waits.
        /// </summary>
        ValueTask Add(Participant participant, LobbyEntry entry);

        /// <summary>
        ///     Remove the participant from the lobby
        /// </summary>
        /// <param name="participant">The participant</param>
        /// <param name="expectedConnectionId">
        ///     If not null, the entry is only removed if it belongs to this connection (a newer
        ///     connection of the same participant may have replaced it)
        /// </param>
        /// <returns>The removed entry or null if the participant was not waiting (or someone else removed it first)</returns>
        ValueTask<LobbyEntry?> TryRemove(Participant participant, string? expectedConnectionId = null);

        ValueTask<IReadOnlyDictionary<string, LobbyEntry>> GetAll(string conferenceId);

        /// <summary>
        ///     Remember that the participant was admitted, so he does not have to wait again if he reconnects
        /// </summary>
        ValueTask MarkAdmitted(Participant participant);

        ValueTask RemoveAdmitted(Participant participant);

        ValueTask<bool> IsAdmitted(Participant participant);
    }
}
