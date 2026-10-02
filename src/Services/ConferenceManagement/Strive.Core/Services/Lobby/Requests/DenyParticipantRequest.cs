using MediatR;
using Strive.Core.Interfaces;

namespace Strive.Core.Services.Lobby.Requests
{
    public record DenyParticipantRequest(Participant Participant) : IRequest<SuccessOrError<Unit>>;
}
