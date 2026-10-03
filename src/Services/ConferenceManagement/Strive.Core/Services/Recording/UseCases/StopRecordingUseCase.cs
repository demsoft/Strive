using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Strive.Core.Interfaces;
using Strive.Core.Services.Recording.Gateways;
using Strive.Core.Services.Recording.Requests;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.Recording.UseCases
{
    public class StopRecordingUseCase : IRequestHandler<StopRecordingRequest, SuccessOrError<Unit>>
    {
        private readonly IMediator _mediator;
        private readonly IRecordingRepo _repository;
        private readonly IRecorderClient _recorder;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<StopRecordingUseCase> _logger;

        public StopRecordingUseCase(IMediator mediator, IRecordingRepo repository, IRecorderClient recorder,
            TimeProvider timeProvider, ILogger<StopRecordingUseCase> logger)
        {
            _mediator = mediator;
            _repository = repository;
            _recorder = recorder;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<SuccessOrError<Unit>> Handle(StopRecordingRequest request,
            CancellationToken cancellationToken)
        {
            var recording = await _repository.FindActiveOfConference(request.ConferenceId);
            if (recording == null) return RecordingError.NotRecording;

            // already stopping, stopping twice is not an error
            if (recording.Status != RecordingStatus.Finalizing)
            {
                recording.Status = RecordingStatus.Finalizing;
                recording.EndedAt = _timeProvider.GetUtcNow();
                await _repository.Update(recording);
                await _mediator.Send(
                    new UpdateSynchronizedObjectRequest(request.ConferenceId, SynchronizedRecording.SyncObjId),
                    cancellationToken);
            }

            try
            {
                await _recorder.Stop(recording.RecordingId, cancellationToken);
            }
            catch (Exception e)
            {
                // the recording stays "finalizing", ExpireRecordings fails it if the recorder never reports back
                _logger.LogError(e, "Could not tell the recorder to stop recording {recordingId}",
                    recording.RecordingId);
            }

            return Unit.Value;
        }
    }
}
