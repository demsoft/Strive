using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Extensions;
using Strive.Core.Services.ConferenceControl.Gateways;
using Strive.Core.Services.ConferenceControl.Requests;
using Strive.Core.Services.Equipment.Gateways;
using Strive.Core.Services.Equipment.Requests;
using Strive.Core.Services.Media.Dtos;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.Equipment.UseCases
{
    public class InitializeEquipmentUseCase : IRequestHandler<InitializeEquipmentRequest>
    {
        private readonly IEquipmentConnectionRepository _repository;
        private readonly IJoinedParticipantsRepository _joinedParticipantsRepository;
        private readonly IMediator _mediator;

        public InitializeEquipmentUseCase(IEquipmentConnectionRepository repository,
            IJoinedParticipantsRepository joinedParticipantsRepository, IMediator mediator)
        {
            _repository = repository;
            _joinedParticipantsRepository = joinedParticipantsRepository;
            _mediator = mediator;
        }

        public async Task Handle(InitializeEquipmentRequest request, CancellationToken cancellationToken)
        {
            var connection = new EquipmentConnection(request.ConnectionId, request.Name, request.Devices,
                ImmutableDictionary<ProducerSource, UseMediaStateInfo>.Empty);

            // the participant left notification, which removes the equipment connections, is published under this lock,
            // so the participant cannot leave between the check and the write
            await using (await _joinedParticipantsRepository.LockParticipantJoin(request.Participant))
            {
                var isJoined = await _mediator.Send(new CheckIsParticipantJoinedRequest(request.Participant));
                if (!isJoined)
                    throw EquipmentError.ParticipantNotJoined.ToException();

                await _repository.SetConnection(request.Participant, connection);
            }

            await _mediator.Send(new UpdateSynchronizedObjectRequest(request.Participant.ConferenceId,
                SynchronizedEquipment.SyncObjId(request.Participant.Id)));
        }
    }
}
