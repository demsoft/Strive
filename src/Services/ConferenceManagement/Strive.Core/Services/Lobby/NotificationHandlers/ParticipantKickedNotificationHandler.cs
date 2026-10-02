using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.ConferenceControl.Notifications;
using Strive.Core.Services.Lobby.Gateways;

namespace Strive.Core.Services.Lobby.NotificationHandlers
{
    /// <summary>
    ///     A participant kicked by a moderator must be admitted again if he tries to return
    /// </summary>
    public class ParticipantKickedNotificationHandler : INotificationHandler<ParticipantKickedNotification>
    {
        private readonly ILobbyRepository _repository;

        public ParticipantKickedNotificationHandler(ILobbyRepository repository)
        {
            _repository = repository;
        }

        public async Task Handle(ParticipantKickedNotification notification, CancellationToken cancellationToken)
        {
            if (notification.Reason == ParticipantKickedReason.ByModerator)
                await _repository.RemoveAdmitted(notification.Participant);
        }
    }
}
