using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.ConferenceControl.Gateways;
using Strive.Core.Services.HandRaise.Gateways;
using Strive.Core.Services.HandRaise.Requests;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.HandRaise.UseCases
{
    public class LowerHandUseCase : IRequestHandler<LowerHandRequest>
    {
        private readonly IHandRaiseRepository _repository;
        private readonly IJoinedParticipantsRepository _joinedParticipantsRepository;
        private readonly IMediator _mediator;

        public LowerHandUseCase(IHandRaiseRepository repository,
            IJoinedParticipantsRepository joinedParticipantsRepository, IMediator mediator)
        {
            _repository = repository;
            _joinedParticipantsRepository = joinedParticipantsRepository;
            _mediator = mediator;
        }

        public async Task Handle(LowerHandRequest request, CancellationToken cancellationToken)
        {
            var participant = request.Participant;

            bool lowered;
            await using (await _joinedParticipantsRepository.LockParticipantJoin(participant))
            {
                lowered = await _repository.Lower(participant);
            }

            if (lowered)
                await _mediator.Send(new UpdateSynchronizedObjectRequest(participant.ConferenceId,
                    SynchronizedHandRaises.SyncObjId), cancellationToken);
        }
    }
}
