using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Strive.Core.Services.Recording.Gateways;
using Strive.Core.Services.Recording.Requests;

namespace Strive.Core.Services.Recording.UseCases
{
    public class CheckRecorderMayJoinUseCase : IRequestHandler<CheckRecorderMayJoinRequest, bool>
    {
        private readonly IRecordingRepo _repository;

        public CheckRecorderMayJoinUseCase(IRecordingRepo repository)
        {
            _repository = repository;
        }

        public async Task<bool> Handle(CheckRecorderMayJoinRequest request, CancellationToken cancellationToken)
        {
            var (participant, recordingId) = request;

            // the participant id is derived from the recording id, so a token cannot be used for another recording
            if (participant.Id != RecorderParticipants.ParticipantId(recordingId)) return false;

            var recording = await _repository.FindById(recordingId);
            return recording is {IsActive: true} && recording.ConferenceId == participant.ConferenceId &&
                   recording.Status != RecordingStatus.Finalizing;
        }
    }
}
