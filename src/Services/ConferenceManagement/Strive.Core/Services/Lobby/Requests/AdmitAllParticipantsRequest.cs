using MediatR;

namespace Strive.Core.Services.Lobby.Requests
{
    public record AdmitAllParticipantsRequest(string ConferenceId) : IRequest;
}
