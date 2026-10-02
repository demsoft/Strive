using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.ConferenceControl.Notifications;

namespace Strive.Core.Services.Reactions
{
    public class ParticipantLeftNotificationHandler : INotificationHandler<ParticipantLeftNotification>
    {
        private readonly IReactionRateLimiter _rateLimiter;

        public ParticipantLeftNotificationHandler(IReactionRateLimiter rateLimiter)
        {
            _rateLimiter = rateLimiter;
        }

        public Task Handle(ParticipantLeftNotification notification, CancellationToken cancellationToken)
        {
            _rateLimiter.Remove(notification.Participant);
            return Task.CompletedTask;
        }
    }
}
