using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.Lobby.Gateways;
using Strive.Core.Services.Lobby.Requests;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.Lobby.UseCases
{
    public class EnterLobbyUseCase : IRequestHandler<EnterLobbyRequest>
    {
        private readonly ILobbyRepository _repository;
        private readonly IMediator _mediator;
        private readonly TimeProvider _timeProvider;

        public EnterLobbyUseCase(ILobbyRepository repository, IMediator mediator, TimeProvider timeProvider)
        {
            _repository = repository;
            _mediator = mediator;
            _timeProvider = timeProvider;
        }

        public async Task Handle(EnterLobbyRequest request, CancellationToken cancellationToken)
        {
            var (participant, connectionId, displayName) = request;

            await _repository.Add(participant, new LobbyEntry(connectionId, displayName, _timeProvider.GetUtcNow()));
            await _mediator.Send(new UpdateSynchronizedObjectRequest(participant.ConferenceId,
                SynchronizedLobby.SyncObjId), cancellationToken);
        }
    }
}
