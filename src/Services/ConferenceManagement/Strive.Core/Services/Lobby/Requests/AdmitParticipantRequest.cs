using MediatR;
using Strive.Core.Interfaces;

namespace Strive.Core.Services.Lobby.Requests
{
    public record AdmitParticipantRequest(Participant Participant) : IRequest<SuccessOrError<Unit>>;
}
