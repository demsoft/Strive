using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.ConferenceControl.Gateways;
using Strive.Core.Services.LessonMode.Gateways;
using Strive.Core.Services.LessonMode.Requests;
using Strive.Core.Services.Permissions.Requests;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.LessonMode.UseCases
{
    public class SetLessonModeUseCase : IRequestHandler<SetLessonModeRequest>
    {
        private readonly IMediator _mediator;
        private readonly ILessonModeRepository _repository;
        private readonly IJoinedParticipantsRepository _participantsRepository;

        public SetLessonModeUseCase(IMediator mediator, ILessonModeRepository repository,
            IJoinedParticipantsRepository participantsRepository)
        {
            _mediator = mediator;
            _repository = repository;
            _participantsRepository = participantsRepository;
        }

        public async Task Handle(SetLessonModeRequest request, CancellationToken cancellationToken)
        {
            var (conferenceId, active) = request;
            if (!await _repository.Set(conferenceId, active)) return;

            // everybody's permissions change (the media server mutes or unmutes them), then the participants are told
            var participants = await _participantsRepository.GetParticipantsOfConference(conferenceId);
            await _mediator.Send(new UpdateParticipantsPermissionsRequest(participants), cancellationToken);
            await _mediator.Send(new UpdateSynchronizedObjectRequest(conferenceId, SynchronizedLessonMode.SyncObjId),
                cancellationToken);
        }
    }
}
