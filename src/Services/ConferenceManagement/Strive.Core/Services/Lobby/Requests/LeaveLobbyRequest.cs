using MediatR;

namespace Strive.Core.Services.Lobby.Requests
{
    /// <summary>
    ///     The connection of a waiting participant was closed
    /// </summary>
    public record LeaveLobbyRequest(Participant Participant, string ConnectionId) : IRequest;
}
