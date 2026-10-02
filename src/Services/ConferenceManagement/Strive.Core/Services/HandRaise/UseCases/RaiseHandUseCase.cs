using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.ConferenceControl.Gateways;
using Strive.Core.Services.HandRaise.Gateways;
using Strive.Core.Services.HandRaise.Requests;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.HandRaise.UseCases
{
    public class RaiseHandUseCase : IRequestHandler<RaiseHandRequest>
    {
        private readonly IHandRaiseRepository _repository;
        private readonly IJoinedParticipantsRepository _joinedParticipantsRepository;
        private readonly IMediator _mediator;
        private readonly TimeProvider _timeProvider;

        public RaiseHandUseCase(IHandRaiseRepository repository,
            IJoinedParticipantsRepository joinedParticipantsRepository, IMediator mediator, TimeProvider timeProvider)
        {
            _repository = repository;
            _joinedParticipantsRepository = joinedParticipantsRepository;
            _mediator = mediator;
            _timeProvider = timeProvider;
        }

        public async Task Handle(RaiseHandRequest request, CancellationToken cancellationToken)
        {
            var participant = request.Participant;

            bool raised;

            // The participant left notification, which removes the raised hand, is published under this lock. Without
            // it, a request that is processed while the participant leaves would put the participant back in the list.
            await using (await _joinedParticipantsRepository.LockParticipantJoin(participant))
            {
                if (!await _joinedParticipantsRepository.IsParticipantJoined(participant)) return;

                raised = await _repository.Raise(participant, _timeProvider.GetUtcNow());
            }

            if (raised)
                await _mediator.Send(new UpdateSynchronizedObjectRequest(participant.ConferenceId,
                    SynchronizedHandRaises.SyncObjId), cancellationToken);
        }
    }
}
