using MediatR;

namespace Strive.Core.Services.Lobby.Requests
{
    public record EnterLobbyRequest(Participant Participant, string ConnectionId, string DisplayName) : IRequest;
}
