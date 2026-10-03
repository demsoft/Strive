using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.Lobby.Gateways;
using Strive.Core.Services.Lobby.Requests;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.Lobby.UseCases
{
    public class LeaveLobbyUseCase : IRequestHandler<LeaveLobbyRequest>
    {
        private readonly ILobbyRepository _repository;
        private readonly IMediator _mediator;

        public LeaveLobbyUseCase(ILobbyRepository repository, IMediator mediator)
        {
            _repository = repository;
            _mediator = mediator;
        }

        public async Task Handle(LeaveLobbyRequest request, CancellationToken cancellationToken)
        {
            var (participant, connectionId) = request;

            if (await _repository.TryRemove(participant, connectionId) != null)
                await _mediator.Send(new UpdateSynchronizedObjectRequest(participant.ConferenceId,
                    SynchronizedLobby.SyncObjId), cancellationToken);
        }
    }
}
