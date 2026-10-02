using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Interfaces;
using Strive.Core.Services.Lobby.Gateways;
using Strive.Core.Services.Lobby.Notifications;
using Strive.Core.Services.Lobby.Requests;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.Lobby.UseCases
{
    public class AdmitParticipantUseCase : IRequestHandler<AdmitParticipantRequest, SuccessOrError<Unit>>
    {
        private readonly ILobbyRepository _repository;
        private readonly IMediator _mediator;

        public AdmitParticipantUseCase(ILobbyRepository repository, IMediator mediator)
        {
            _repository = repository;
            _mediator = mediator;
        }

        public async Task<SuccessOrError<Unit>> Handle(AdmitParticipantRequest request,
            CancellationToken cancellationToken)
        {
            var participant = request.Participant;

            // only one moderator can win if multiple admit/deny the same participant
            var entry = await _repository.TryRemove(participant);
            if (entry == null) return LobbyError.ParticipantNotWaiting;

            await _repository.MarkAdmitted(participant);
            await _mediator.Send(new UpdateSynchronizedObjectRequest(participant.ConferenceId,
                SynchronizedLobby.SyncObjId), cancellationToken);

            await _mediator.Publish(
                new ParticipantAdmittedNotification(participant, entry.ConnectionId, entry.DisplayName),
                cancellationToken);

            return Unit.Value;
        }
    }
}
