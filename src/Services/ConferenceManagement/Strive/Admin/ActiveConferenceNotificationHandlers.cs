using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.ConferenceControl.Notifications;

namespace Strive.Admin
{
    /// <summary>Tells the tracker of the admin overview what happens in the conferences.</summary>
    public class ActiveConferenceNotificationHandlers : INotificationHandler<ParticipantJoinedNotification>,
        INotificationHandler<ParticipantLeftNotification>, INotificationHandler<ConferenceOpenedNotification>,
        INotificationHandler<ConferenceClosedNotification>
    {
        private readonly ActiveConferenceTracker _tracker;

        public ActiveConferenceNotificationHandlers(ActiveConferenceTracker tracker)
        {
            _tracker = tracker;
        }

        public Task Handle(ConferenceClosedNotification notification, CancellationToken cancellationToken)
        {
            _tracker.ConferenceClosed(notification.ConferenceId);
            return Task.CompletedTask;
        }

        public Task Handle(ConferenceOpenedNotification notification, CancellationToken cancellationToken)
        {
            _tracker.ConferenceOpened(notification.ConferenceId);
            return Task.CompletedTask;
        }

        public Task Handle(ParticipantJoinedNotification notification, CancellationToken cancellationToken)
        {
            _tracker.ParticipantJoined(notification.Participant.ConferenceId, notification.Participant.Id);
            return Task.CompletedTask;
        }

        public Task Handle(ParticipantLeftNotification notification, CancellationToken cancellationToken)
        {
            _tracker.ParticipantLeft(notification.Participant.ConferenceId, notification.Participant.Id);
            return Task.CompletedTask;
        }
    }
}
