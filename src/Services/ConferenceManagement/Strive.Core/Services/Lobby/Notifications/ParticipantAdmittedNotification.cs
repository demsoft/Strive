using MediatR;

namespace Strive.Core.Services.Lobby.Notifications
{
    /// <summary>
    ///     A waiting participant was admitted, his connection must now join the conference
    /// </summary>
    public record ParticipantAdmittedNotification(Participant Participant, string ConnectionId, string DisplayName) :
        INotification;
}
