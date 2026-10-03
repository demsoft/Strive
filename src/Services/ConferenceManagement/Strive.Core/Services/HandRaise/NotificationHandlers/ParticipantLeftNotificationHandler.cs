using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.ConferenceControl.Notifications;
using Strive.Core.Services.HandRaise.Gateways;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.HandRaise.NotificationHandlers
{
    public class ParticipantLeftNotificationHandler : INotificationHandler<ParticipantLeftNotification>
    {
        private readonly IHandRaiseRepository _repository;
        private readonly IMediator _mediator;

        public ParticipantLeftNotificationHandler(IHandRaiseRepository repository, IMediator mediator)
        {
            _repository = repository;
            _mediator = mediator;
        }

        public async Task Handle(ParticipantLeftNotification notification, CancellationToken cancellationToken)
        {
            // published under the participant join lock
            if (await _repository.Lower(notification.Participant))
                await _mediator.Send(new UpdateSynchronizedObjectRequest(notification.Participant.ConferenceId,
                    SynchronizedHandRaises.SyncObjId), cancellationToken);
        }
    }
}
