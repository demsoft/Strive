using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.Lobby.Gateways;
using Strive.Core.Services.Lobby.Requests;

namespace Strive.Core.Services.Lobby.UseCases
{
    public class AdmitAllParticipantsUseCase : IRequestHandler<AdmitAllParticipantsRequest>
    {
        private readonly ILobbyRepository _repository;
        private readonly IMediator _mediator;

        public AdmitAllParticipantsUseCase(ILobbyRepository repository, IMediator mediator)
        {
            _repository = repository;
            _mediator = mediator;
        }

        public async Task Handle(AdmitAllParticipantsRequest request, CancellationToken cancellationToken)
        {
            var waiting = await _repository.GetAll(request.ConferenceId);

            foreach (var participantId in waiting.Keys)
                await _mediator.Send(new AdmitParticipantRequest(new Participant(request.ConferenceId, participantId)),
                    cancellationToken);
        }
    }
}
