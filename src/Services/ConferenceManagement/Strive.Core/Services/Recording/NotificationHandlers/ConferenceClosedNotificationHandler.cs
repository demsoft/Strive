using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.ConferenceControl.Notifications;
using Strive.Core.Services.Recording.Requests;

namespace Strive.Core.Services.Recording.NotificationHandlers
{
    /// <summary>
    ///     Nobody is left to record when the conference is closed
    /// </summary>
    public class ConferenceClosedNotificationHandler : INotificationHandler<ConferenceClosedNotification>
    {
        private readonly IMediator _mediator;

        public ConferenceClosedNotificationHandler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task Handle(ConferenceClosedNotification notification, CancellationToken cancellationToken)
        {
            // an error because nothing is recorded is expected and ignored
            await _mediator.Send(new StopRecordingRequest(notification.ConferenceId), cancellationToken);
        }
    }
}
