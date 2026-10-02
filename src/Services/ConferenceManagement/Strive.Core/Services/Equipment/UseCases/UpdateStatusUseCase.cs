using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Extensions;
using Strive.Core.Services.ConferenceControl.Gateways;
using Strive.Core.Services.Equipment.Gateways;
using Strive.Core.Services.Equipment.Requests;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.Equipment.UseCases
{
    public class UpdateStatusUseCase : IRequestHandler<UpdateStatusRequest>
    {
        private readonly IEquipmentConnectionRepository _repository;
        private readonly IJoinedParticipantsRepository _joinedParticipantsRepository;
        private readonly IMediator _mediator;

        public UpdateStatusUseCase(IEquipmentConnectionRepository repository,
            IJoinedParticipantsRepository joinedParticipantsRepository, IMediator mediator)
        {
            _repository = repository;
            _joinedParticipantsRepository = joinedParticipantsRepository;
            _mediator = mediator;
        }

        public async Task Handle(UpdateStatusRequest request, CancellationToken cancellationToken)
        {
            var (participant, connectionId, status) = request;

            // the participant left notification, which removes the equipment connections, is published under this lock.
            // Without it, an update that read the connection before the removal would write it back afterwards.
            await using (await _joinedParticipantsRepository.LockParticipantJoin(participant))
            {
                var connection = await _repository.GetConnection(participant, connectionId);
                if (connection == null)
                    throw EquipmentError.NotInitialized.ToException();

                var updatedConnection = connection with {Status = status};
                await _repository.SetConnection(participant, updatedConnection);
            }

            await _mediator.Send(new UpdateSynchronizedObjectRequest(participant.ConferenceId,
                SynchronizedEquipment.SyncObjId(participant.Id)));
        }
    }
}
