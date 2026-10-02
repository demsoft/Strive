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
    public class DenyParticipantUseCase : IRequestHandler<DenyParticipantRequest, SuccessOrError<Unit>>
    {
        private readonly ILobbyRepository _repository;
        private readonly IMediator _mediator;

        public DenyParticipantUseCase(ILobbyRepository repository, IMediator mediator)
        {
            _repository = repository;
            _mediator = mediator;
        }

        public async Task<SuccessOrError<Unit>> Handle(DenyParticipantRequest request,
            CancellationToken cancellationToken)
        {
            var participant = request.Participant;

            var entry = await _repository.TryRemove(participant);
            if (entry == null) return LobbyError.ParticipantNotWaiting;

            await _mediator.Send(new UpdateSynchronizedObjectRequest(participant.ConferenceId,
                SynchronizedLobby.SyncObjId), cancellationToken);

            await _mediator.Publish(new ParticipantDeniedNotification(participant, entry.ConnectionId),
                cancellationToken);

            return Unit.Value;
        }
    }
}
