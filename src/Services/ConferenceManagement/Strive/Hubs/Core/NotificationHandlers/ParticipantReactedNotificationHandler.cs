using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Strive.Core.Services.Reactions.Notifications;
using Strive.Hubs.Core.Responses;

namespace Strive.Hubs.Core.NotificationHandlers
{
    public class ParticipantReactedNotificationHandler : INotificationHandler<ParticipantReactedNotification>
    {
        private readonly IHubContext<CoreHub> _hubContext;

        public ParticipantReactedNotificationHandler(IHubContext<CoreHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task Handle(ParticipantReactedNotification notification, CancellationToken cancellationToken)
        {
            var (_, recipients, sender, emoji, timestamp) = notification;

            var groups = recipients.Select(CoreHubGroups.OfParticipant);
            await _hubContext.Clients.Groups(groups)
                .Reaction(new ReactionDto(sender.Id, emoji, timestamp), cancellationToken);
        }
    }
}
