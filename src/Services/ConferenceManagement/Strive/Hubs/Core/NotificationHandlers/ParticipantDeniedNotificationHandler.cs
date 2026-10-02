using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Strive.Core.Services.Lobby.Notifications;
using Strive.Hubs.Core.Responses;

namespace Strive.Hubs.Core.NotificationHandlers
{
    public class ParticipantDeniedNotificationHandler : INotificationHandler<ParticipantDeniedNotification>
    {
        private readonly IHubContext<CoreHub> _hubContext;

        public ParticipantDeniedNotificationHandler(IHubContext<CoreHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task Handle(ParticipantDeniedNotification notification, CancellationToken cancellationToken)
        {
            await _hubContext.Clients.Client(notification.ConnectionId)
                .LobbyStatus(new LobbyStatusDto(LobbyStatus.Denied), cancellationToken);
        }
    }
}
