using MediatR;

namespace Strive.Core.Services.Lobby.Requests
{
    /// <summary>
    ///     Check whether the participant has to wait in the lobby before joining
    /// </summary>
    public record ShouldWaitInLobbyRequest(Participant Participant) : IRequest<bool>;
}
