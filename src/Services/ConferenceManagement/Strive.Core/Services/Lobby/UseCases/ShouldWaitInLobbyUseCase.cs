using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.ConferenceControl.Gateways;
using Strive.Core.Services.ConferenceManagement.Requests;
using Strive.Core.Services.Lobby.Gateways;
using Strive.Core.Services.Lobby.Requests;

namespace Strive.Core.Services.Lobby.UseCases
{
    public class ShouldWaitInLobbyUseCase : IRequestHandler<ShouldWaitInLobbyRequest, bool>
    {
        private readonly IMediator _mediator;
        private readonly IOpenConferenceRepository _openConferenceRepository;
        private readonly ILobbyRepository _lobbyRepository;

        public ShouldWaitInLobbyUseCase(IMediator mediator, IOpenConferenceRepository openConferenceRepository,
            ILobbyRepository lobbyRepository)
        {
            _mediator = mediator;
            _openConferenceRepository = openConferenceRepository;
            _lobbyRepository = lobbyRepository;
        }

        public async Task<bool> Handle(ShouldWaitInLobbyRequest request, CancellationToken cancellationToken)
        {
            var participant = request.Participant;

            var conference = await _mediator.Send(new FindConferenceByIdRequest(participant.ConferenceId),
                cancellationToken);

            if (!conference.Configuration.Lobby.IsEnabled) return false;
            if (conference.Configuration.Moderators.Contains(participant.Id)) return false;

            // the lobby only controls access to a running conference, until then everybody sees that it is not open
            if (!await _openConferenceRepository.IsOpen(participant.ConferenceId)) return false;

            return !await _lobbyRepository.IsAdmitted(participant);
        }
    }
}
