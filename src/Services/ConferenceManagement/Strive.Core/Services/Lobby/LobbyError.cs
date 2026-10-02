using Strive.Core.Dto;
using Strive.Core.Errors;

namespace Strive.Core.Services.Lobby
{
    public class LobbyError : ErrorsProvider<ServiceErrorCode>
    {
        public static Error ParticipantNotWaiting =>
            NotFound("The participant is not waiting in the lobby.", ServiceErrorCode.Lobby_ParticipantNotWaiting);
    }
}
