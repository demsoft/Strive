using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.ConferenceControl.Notifications;
using Strive.Core.Services.Latency.Gateways;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.Latency.NotificationHandlers
{
    public class ParticipantLeftNotificationHandler : INotificationHandler<ParticipantLeftNotification>
    {
        private readonly ILatencyRepository _repository;
        private readonly IMediator _mediator;

        public ParticipantLeftNotificationHandler(ILatencyRepository repository, IMediator mediator)
        {
            _repository = repository;
            _mediator = mediator;
        }

        public async Task Handle(ParticipantLeftNotification notification, CancellationToken cancellationToken)
        {
            if (await _repository.Remove(notification.Participant))
                await _mediator.Send(new UpdateSynchronizedObjectRequest(notification.Participant.ConferenceId,
                    SynchronizedLatency.SyncObjId), cancellationToken);
        }
    }
}
