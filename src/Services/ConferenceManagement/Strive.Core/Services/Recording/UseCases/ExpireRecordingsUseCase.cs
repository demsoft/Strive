using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Strive.Core.Services.Recording.Gateways;
using Strive.Core.Services.Recording.Requests;
using Strive.Core.Services.Synchronization.Requests;

namespace Strive.Core.Services.Recording.UseCases
{
    public class ExpireRecordingsUseCase : IRequestHandler<ExpireRecordingsRequest>
    {
        private readonly IMediator _mediator;
        private readonly IRecordingRepo _repository;
        private readonly IRecordingStorage _storage;
        private readonly RecordingOptions _options;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ExpireRecordingsUseCase> _logger;

        public ExpireRecordingsUseCase(IMediator mediator, IRecordingRepo repository, IRecordingStorage storage,
            IOptions<RecordingOptions> options, TimeProvider timeProvider, ILogger<ExpireRecordingsUseCase> logger)
        {
            _mediator = mediator;
            _repository = repository;
            _storage = storage;
            _options = options.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(ExpireRecordingsRequest request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow();

            // the recorder stops by itself after the maximum duration, if it did not report back shortly after that,
            // it is gone
            var stuckBefore = now.AddMinutes(-(_options.MaxDurationMinutes + 30));
            foreach (var recording in await _repository.FindUnfinishedStartedBefore(stuckBefore))
            {
                _logger.LogWarning("Recording {recordingId} never finished, mark it as failed", recording.RecordingId);

                recording.Status = RecordingStatus.Failed;
                recording.FailureReason = "The recorder did not report back.";
                recording.EndedAt ??= now;
                await _repository.Update(recording);
                await _mediator.Send(
                    new UpdateSynchronizedObjectRequest(recording.ConferenceId, SynchronizedRecording.SyncObjId),
                    cancellationToken);
            }

            foreach (var recording in await _repository.FindExpired(now))
            {
                try
                {
                    if (recording.StorageKey != null)
                        await _storage.Delete(recording.StorageKey, cancellationToken);
                    await _repository.Delete(recording.RecordingId);

                    _logger.LogInformation("Deleted expired recording {recordingId}", recording.RecordingId);
                }
                catch (Exception e)
                {
                    // try again at the next run
                    _logger.LogError(e, "Could not delete expired recording {recordingId}", recording.RecordingId);
                }
            }
        }
    }
}
