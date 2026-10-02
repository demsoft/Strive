using MediatR;

namespace Strive.Core.Services.Lobby.Notifications
{
    public record ParticipantDeniedNotification(Participant Participant, string ConnectionId) : INotification;
}
